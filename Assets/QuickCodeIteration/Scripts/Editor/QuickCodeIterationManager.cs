using System;
using System.CodeDom;
using System.Reflection;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CSharp;
using QuickCodeIteration.Scripts.Runtime;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
[PreventHotReload]
public class QuickCodeIterationManager
{
    private static QuickCodeIterationManager _instance;
    public static QuickCodeIterationManager Instance => _instance ?? (_instance = new QuickCodeIterationManager());

    private PlayModeStateChange _lastPlayModeStateChange;
    private List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();

    private List<DynamicFileHotReloadState> _dynamicFileHotReloadStateEntries = new List<DynamicFileHotReloadState>();

    private float _batchChangesEveryNSeconds = 5f; //TODO: expose, and make larger by default
    private DateTime _lastTimeChangeBatchRun = default(DateTime);
    private bool _executeOnlyInPlaymode = true; //TODO: potentially later add editor support - needed?
    
    private void OnWatchedFileChange(object source, FileSystemEventArgs e)
    {
        if (_lastPlayModeStateChange != PlayModeStateChange.EnteredPlayMode)
        {
#if QuickCodeIterationManager_DebugEnabled
            Debug.Log($"Application not playing, change to: {e.Name} won't be compiled and hot reloaded"); //TODO: remove when not in testing?
#endif
            return;
        }
        
        _dynamicFileHotReloadStateEntries.Add(new DynamicFileHotReloadState(e.FullPath, DateTime.UtcNow));
    }

    public void StartWatchingDirectoryAndSubdirectories(string directoryPath) 
    {
        var fileWatcher = new FileSystemWatcher();
        fileWatcher.Path = new FileInfo(directoryPath).Directory.FullName;
        fileWatcher.IncludeSubdirectories = true;
        fileWatcher.Filter =  "*.cs";
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnWatchedFileChange;
        
        fileWatcher.EnableRaisingEvents = true;
        
        _fileWatchers.Add(fileWatcher);
    }
    
    public void StartWatchingSingleFile(string fullFilePath) 
    {
        var fileWatcher = new FileSystemWatcher();
        var fileToWatch = new FileInfo(fullFilePath);
        fileWatcher.Path = fileToWatch.Directory.FullName;
        fileWatcher.Filter = fileToWatch.Name;
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnWatchedFileChange;
        
        fileWatcher.EnableRaisingEvents = true;
        
        _fileWatchers.Add(fileWatcher);
    }

    static QuickCodeIterationManager()
    {
        EditorApplication.update += Instance.Update;
        EditorApplication.playModeStateChanged += Instance.OnEditorApplicationOnplayModeStateChanged;
        
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Instance.StartWatchingDirectoryAndSubdirectories(Application.dataPath);
        }
    }

    private void Update()
    {
        if (_executeOnlyInPlaymode && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }
        
        var assemblyChangesLoader = AssemblyChangesLoaderResolver.Instance.Resolve(); //WARN: need to resolve initially in case monobehaviour singleton is not created
        if ((DateTime.UtcNow - _lastTimeChangeBatchRun).TotalSeconds > _batchChangesEveryNSeconds)
        {
            Debug.Log("Batch run");
            
             var changesAwaitingHotReload = _dynamicFileHotReloadStateEntries
                .Where(e => e.IsAwaitingCompilation)
                .ToList();

            if (changesAwaitingHotReload.Any())
            {
                List<string> sourceCodeFilesWithUniqueChangesAwaitingHotReload = null;
                try
                {
                    var sw = new Stopwatch();
                    sw.Start();
                    
                    sourceCodeFilesWithUniqueChangesAwaitingHotReload = changesAwaitingHotReload.GroupBy(e => e.FullFileName)
                        .Select(e => e.First().FullFileName).ToList();

                    var dynamicallyLoadedAssemblyCompilerResult = Compile(sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(File.ReadAllText).ToList(), false);
                    Debug.Log($"Files: {string.Join(",", sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(fn => new FileInfo(fn).Name))} changed - compilation (took {sw.ElapsedMilliseconds}ms)");
                    if (!dynamicallyLoadedAssemblyCompilerResult.Errors.HasErrors)
                    {
                        changesAwaitingHotReload.ForEach(c =>
                        {
                            c.FileCompiledOn = DateTime.UtcNow;
                            c.AssemblyNameCompiledIn = dynamicallyLoadedAssemblyCompilerResult.CompiledAssembly.FullName;
                        });
                        
                        //TODO: return some proper results to make sure entries are correctly updated
                        assemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(dynamicallyLoadedAssemblyCompilerResult.CompiledAssembly);
                        changesAwaitingHotReload.ForEach(c => c.HotSwappedOn = DateTime.UtcNow); //TODO: technically not all were hot swapped at same time
                    }
                    else
                    {
                        if (dynamicallyLoadedAssemblyCompilerResult.Errors.Count > 0) {
                            var msg = new StringBuilder();
                            foreach (CompilerError error in dynamicallyLoadedAssemblyCompilerResult.Errors) {
                                msg.Append($"Error  when compiling, it's best to check code and make sure it's compilable (and also not using C# language feature set that is not supported, eg ??=\r\n line:{error.Line} ({error.ErrorNumber}): {error.ErrorText}\n");
                            }
                            var errorMessage = msg.ToString();
                            
                            changesAwaitingHotReload.ForEach(c =>
                            {
                                c.ErrorOn = DateTime.UtcNow;
                                c.ErrorText = errorMessage;
                            });

                            throw new Exception();
                        } 
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error when updating files: '{(sourceCodeFilesWithUniqueChangesAwaitingHotReload != null ? string.Join(",",sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(fn => new FileInfo(fn).Name)): "unknown")}', {ex}");
                }
                
            }
            
            _lastTimeChangeBatchRun = DateTime.UtcNow;
        }
    }

    private void OnEditorApplicationOnplayModeStateChanged(PlayModeStateChange obj)
    {
        Instance._lastPlayModeStateChange = obj;

        if (obj == PlayModeStateChange.ExitingPlayMode && Instance._fileWatchers.Any())
        {
            foreach (var fileWatcher in Instance._fileWatchers)
            {
                fileWatcher.Dispose();
            }
            Instance._fileWatchers.Clear();
        }
    }
    
    public void DynamicallyUpdateMethodsInWatchedFile(string fullFilePath)
    {
        throw  new NotImplementedException("//TODO: expose API methods that people can easily use, eg force update for file"); //TODO <
    }

    public static CompilerResults Compile(List<string> fileSourceCode, bool compileOnlyInMemory)
    {
        var providerOptions = new Dictionary<string, string>();
        var provider = new CSharpCodeProvider(providerOptions);
        var param = new CompilerParameters();

        var excludeAssyNames = new List<string> //TODO: move out to field/ separate class
        {
            "mscorlib"
        };
        
        var referencesToAdd = new List<string>();
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                     .Where(a => excludeAssyNames.All(assyName => !a.FullName.StartsWith(assyName) 
                                                                  && a.GetCustomAttribute<DynamicallyCreatedAssemblyAttribute>() == null))) 
        {
            try
            {
                if (string.IsNullOrEmpty(assembly.Location))
                {
                    throw new Exception("Assembly location is null");
                }
                referencesToAdd.Add(assembly.Location);;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Unable to add a reference to assembly as unable to get location or null: {assembly.FullName} when hot-reloading, this is likely dynamic assembly and won't cause issues");
            }
        }

        //TODO: work out why 120? is it for every project, or also dependant on other factors like actual project location?
        //TODO: it's not 120 - for main project it seemed to be but for this one is not, something else is at play - need to work out
        const int MaxPathCharsInReferenceLocationBeforeExceptionThrown = 250; 
        foreach (var referenceToAdd in referencesToAdd.Where(r => r.Length < MaxPathCharsInReferenceLocationBeforeExceptionThrown))
        {
            param.ReferencedAssemblies.Add(referenceToAdd);
        }
        
        var referencesOverPathLenghtLimit = referencesToAdd.Where(r => r.Length >= MaxPathCharsInReferenceLocationBeforeExceptionThrown).ToList();
        if (referencesOverPathLenghtLimit.Count > 0)
        {
            Debug.LogWarning($"Assembly references locations are over allowed {MaxPathCharsInReferenceLocationBeforeExceptionThrown} this seems to be existing limitation which will prevent assembly from being compiled," +
                             $"currently there's no known fix - if possible moving those assembles (probably whole project) to root level of drive and shortening project folder name could help." +
                             $"\r\nReferences:{string.Join(Environment.NewLine, referencesOverPathLenghtLimit)}");
        }
        
        param.GenerateExecutable = false;
        param.GenerateInMemory = compileOnlyInMemory;

        var dynamicallyCreatedAssemblyAttributeSourceCore = GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(param, provider, typeof(DynamicallyCreatedAssemblyAttribute), Guid.NewGuid().ToString());
        
        //prevent namespace clash, and add new lines to ensure code doesn't end / start with a comment which would cause compilation issues, nested namespaces are fine
        var sourceCodeNestedInNamespaceToPreventSameTypeClash = fileSourceCode.Select(fSc => $"namespace {AssemblyChangesLoader.NAMESPACE_ADDED_FOR_CREATED_CLASS}{Environment.NewLine}{{{fSc} {Environment.NewLine}}}");
        var sourceCodeCombined = string.Join(Environment.NewLine, sourceCodeNestedInNamespaceToPreventSameTypeClash);
        return provider.CompileAssemblyFromSource(param, sourceCodeCombined, dynamicallyCreatedAssemblyAttributeSourceCore);
    }

    private static string GenerateSourceCodeForAddCustomAttributeToGeneratedAssembly(CompilerParameters param, CSharpCodeProvider provider, Type customAttributeType, 
        string customAttributeStringCtorParam) //warn: not very reusable to force single string param like that
    {
        var dynamicallyCreatedAssemblyAttributeAssemblyLocation = typeof(DynamicallyCreatedAssemblyAttribute).Assembly.Location;
        if (param.ReferencedAssemblies.Contains(dynamicallyCreatedAssemblyAttributeAssemblyLocation))
        {
            param.ReferencedAssemblies.Add(dynamicallyCreatedAssemblyAttributeAssemblyLocation);
        }

        var unit = new CodeCompileUnit();
        var attr = new CodeTypeReference(customAttributeType);
        var decl = new CodeAttributeDeclaration(attr, new CodeAttributeArgument(new CodePrimitiveExpression(customAttributeStringCtorParam)));
        unit.AssemblyCustomAttributes.Add(decl);
        var assemblyInfo = new StringWriter();
        provider.GenerateCodeFromCompileUnit(unit, assemblyInfo, new CodeGeneratorOptions());
        return assemblyInfo.ToString();
    }
}

public class DynamicFileHotReloadState
{
    public string FullFileName { get; set; }
    public DateTime FileChangedOn { get; set; }
    public bool IsAwaitingCompilation => !IsFileCompiled && !ErrorOn.HasValue;
    public bool IsFileCompiled => FileCompiledOn.HasValue;
    public DateTime? FileCompiledOn { get; set; }
    
    public string AssemblyNameCompiledIn { get; set; }

    public bool IsAwaitingHotSwap => IsFileCompiled && !HotSwappedOn.HasValue;
    public DateTime? HotSwappedOn { get; set; }
    public bool IsChangeHotSwapped {get; set; }
    
    public string ErrorText { get; set; }
    public DateTime? ErrorOn { get; set; }

    public DynamicFileHotReloadState(string fullFileName, DateTime fileChangedOn)
    {
        FullFileName = fullFileName;
        FileChangedOn = fileChangedOn;
    }
}