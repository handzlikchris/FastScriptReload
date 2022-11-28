using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using ImmersiveVRTools.Runtime.Common;
using QuickCodeIteration.Scripts.Runtime;
using UnityEditor;
using UnityEngine;
using Debug = global::UnityEngine.Debug;

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
            var changesAwaitingHotReload = _dynamicFileHotReloadStateEntries
                .Where(e => e.IsAwaitingCompilation)
                .ToList();

            if (changesAwaitingHotReload.Any())
            {
                changesAwaitingHotReload.ForEach(c =>
                {
                    c.IsBeingProcessed = true;
                });
                
                UnityMainThreadDispatcher.Instance.EnsureInitialized();
                Task.Run(() =>
                {
                    List<string> sourceCodeFilesWithUniqueChangesAwaitingHotReload = null;
                    try
                    {
                        sourceCodeFilesWithUniqueChangesAwaitingHotReload = changesAwaitingHotReload.GroupBy(e => e.FullFileName)
                            .Select(e => e.First().FullFileName).ToList();

                        DynamicAssemblyCompiler.Compile(sourceCodeFilesWithUniqueChangesAwaitingHotReload);
                        var dynamicallyLoadedAssemblyCompilerResult = DynamicAssemblyCompiler.Compile(sourceCodeFilesWithUniqueChangesAwaitingHotReload);
                        if (!dynamicallyLoadedAssemblyCompilerResult.IsError)
                        {
                            changesAwaitingHotReload.ForEach(c =>
                            {
                                c.FileCompiledOn = DateTime.UtcNow;
                                c.AssemblyNameCompiledIn = dynamicallyLoadedAssemblyCompilerResult.CompiledAssemblyPath;
                            });
                            
                            //TODO: return some proper results to make sure entries are correctly updated
                            assemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(dynamicallyLoadedAssemblyCompilerResult.CompiledAssembly);
                            changesAwaitingHotReload.ForEach(c =>
                            {
                                c.HotSwappedOn = DateTime.UtcNow;
                                c.IsBeingProcessed = false;
                            }); //TODO: technically not all were hot swapped at same time
                        }
                        else
                        {
                            if (dynamicallyLoadedAssemblyCompilerResult.MessagesFromCompilerProcess.Count > 0) {
                                var msg = new StringBuilder();
                                foreach (string message in dynamicallyLoadedAssemblyCompilerResult.MessagesFromCompilerProcess) {
                                    msg.AppendLine($"Error  when compiling, it's best to check code and make sure it's compilable \r\n {message}\n");
                                }
                                var errorMessage = msg.ToString();
                                
                                changesAwaitingHotReload.ForEach(c =>
                                {
                                    c.ErrorOn = DateTime.UtcNow;
                                    c.ErrorText = errorMessage;
                                });

                                throw new Exception(errorMessage);
                            } 
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error when updating files: '{(sourceCodeFilesWithUniqueChangesAwaitingHotReload != null ? string.Join(",",sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(fn => new FileInfo(fn).Name)): "unknown")}', {ex}");
                    }
                });
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
}

public class DynamicFileHotReloadState
{
    public string FullFileName { get; set; }
    public DateTime FileChangedOn { get; set; }
    public bool IsAwaitingCompilation => !IsFileCompiled && !ErrorOn.HasValue && !IsBeingProcessed;
    public bool IsFileCompiled => FileCompiledOn.HasValue;
    public DateTime? FileCompiledOn { get; set; }
    
    public string AssemblyNameCompiledIn { get; set; }

    public bool IsAwaitingHotSwap => IsFileCompiled && !HotSwappedOn.HasValue;
    public DateTime? HotSwappedOn { get; set; }
    public bool IsChangeHotSwapped {get; set; }
    
    public string ErrorText { get; set; }
    public DateTime? ErrorOn { get; set; }
    public bool IsBeingProcessed { get; set; }

    public DynamicFileHotReloadState(string fullFileName, DateTime fileChangedOn)
    {
        FullFileName = fullFileName;
        FileChangedOn = fileChangedOn;
    }
}