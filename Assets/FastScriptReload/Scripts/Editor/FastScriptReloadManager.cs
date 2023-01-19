using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastScriptReload.Editor.Compilation;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Runtime.Common;
using ImmersiveVrToolsCommon.Runtime.Logging;
using UnityEditor;
using UnityEngine;

namespace FastScriptReload.Editor
{
    [InitializeOnLoad]
    [PreventHotReload]
    public class FastScriptReloadManager
    {
        private static FastScriptReloadManager _instance;
        public static FastScriptReloadManager Instance => _instance ?? (_instance = new FastScriptReloadManager());
        private static string DataPath = Application.dataPath;

        public const string FileWatcherReplacementTokenForApplicationDataPath = "<Application.dataPath>";
        public Dictionary<string, Func<string>> FileWatcherTokensToResolvePathFn = new Dictionary<string, Func<string>>
        {
            [FileWatcherReplacementTokenForApplicationDataPath] = () => DataPath
        };

        private PlayModeStateChange _lastPlayModeStateChange;
        private List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
        private IEnumerable<string> _currentFileExclusions;
        public bool EnableExperimentalThisCallLimitationFix { get; set; }
        public AssemblyChangesLoaderEditorOptionsNeededInBuild AssemblyChangesLoaderEditorOptionsNeededInBuild { get; private set; } = new AssemblyChangesLoaderEditorOptionsNeededInBuild();

        private List<DynamicFileHotReloadState> _dynamicFileHotReloadStateEntries = new List<DynamicFileHotReloadState>();

        private DateTime _lastTimeChangeBatchRun = default(DateTime);
        private bool _executeOnlyInPlaymode = true; //TODO: potentially later add editor support - needed?
        private bool _assemblyChangesLoaderResolverResolutionAlreadyCalled;
        
        private void OnWatchedFileChange(object source, FileSystemEventArgs e)
        {
            if (_lastPlayModeStateChange != PlayModeStateChange.EnteredPlayMode)
            {
#if ImmersiveVrTools_DebugEnabled
            Debug.Log($"Application not playing, change to: {e.Name} won't be compiled and hot reloaded");
#endif
                return;
            }

            var filePathToUse = e.FullPath;
            if (!File.Exists(filePathToUse))
            {
                Debug.LogWarning(@"Fast Script Reload - Unity File Path Bug - Warning!
Path for changed file passed by Unity does not exist. This is a known editor bug, more info: https://issuetracker.unity3d.com/issues/filesystemwatcher-returns-bad-file-path
                    
Best course of action is to update editor as issue is already fixed in newer (minor and major) versions.
                    
As a workaround asset will try to resolve paths via directory search.
                    
Workaround will search in all folders (under project root) and will use first found file. This means it's possible it'll pick up wrong file as there's no directory information available.");
                
                var changedFileName = new FileInfo(filePathToUse).Name;
                //TODO: try to look in all file watcher configured paths, some users might have code outside of assets, eg packages
                // var fileFoundInAssets = FastScriptReloadPreference.FileWatcherSetupEntries.GetElementsTyped().SelectMany(setupEntries => Directory.GetFiles(DataPath, setupEntries.path, SearchOption.AllDirectories)).ToList();
                    
                var fileFoundInAssets = Directory.GetFiles(DataPath, changedFileName, SearchOption.AllDirectories);
                if (fileFoundInAssets.Length == 0)
                {
                    Debug.LogError($"FileWatcherBugWorkaround: Unable to find file '{changedFileName}', changes will not be reloaded. Please update unity editor.");
                    return;
                }
                else if(fileFoundInAssets.Length  == 1)
                {
                    Debug.Log($"FileWatcherBugWorkaround: Original Unity passed file path: '{e.FullPath}' adjusted to found: '{fileFoundInAssets[0]}'");
                    filePathToUse = fileFoundInAssets[0];
                }
                else
                {
                    Debug.LogWarning($"FileWatcherBugWorkaround: Multiple files found. Original Unity passed file path: '{e.FullPath}' adjusted to found: '{fileFoundInAssets[0]}'");
                    filePathToUse = fileFoundInAssets[0];
                }
            }

            if (_currentFileExclusions != null && _currentFileExclusions.Any(fp => filePathToUse.Replace("\\", "/").EndsWith(fp)))
            {
                Debug.LogWarning($"FastScriptReload: File: '{filePathToUse}' changed, but marked as exclusion. Hot-Reload will not be performed. You can manage exclusions via" +
                                 $"\r\nRight click context menu (Fast Script Reload > Add / Remove Hot-Reload exclusion)" +
                                 $"\r\nor via Window -> Fast Script Reload -> Start Screen -> Exclusion menu");
            
                return;
            }
            
            const int msThresholdToConsiderSameChangeFromDifferentFileWatchers = 500;
            var isDuplicatedChangesComingFromDifferentFileWatcher = _dynamicFileHotReloadStateEntries
                .Any(f => f.FullFileName == filePathToUse
                          && (DateTime.UtcNow - f.FileChangedOn).TotalMilliseconds < msThresholdToConsiderSameChangeFromDifferentFileWatchers);
            if (isDuplicatedChangesComingFromDifferentFileWatcher)
            {
                Debug.LogWarning($"FastScriptReload: Looks like change to: {filePathToUse} have already been added for processing. This can happen if you have multiple file watchers set in a way that they overlap.");
                return;
            }

            _dynamicFileHotReloadStateEntries.Add(new DynamicFileHotReloadState(filePathToUse, DateTime.UtcNow));
        }

        public void StartWatchingDirectoryAndSubdirectories(string directoryPath, string filter, bool includeSubdirectories) 
        {
            foreach (var kv in FileWatcherTokensToResolvePathFn)
            {
                directoryPath = directoryPath.Replace(kv.Key, kv.Value());
            }
            
            var directoryInfo = new DirectoryInfo(directoryPath);

            if (!directoryInfo.Exists)
            {
                Debug.LogWarning($"FastScriptReload: Directory: '{directoryPath}' does not exist, make sure file-watcher setup is correct. You can access via: Window -> Fast Script Reload -> File Watcher (Advanced Setup)");
            }
            
            var fileWatcher = new FileSystemWatcher();

            fileWatcher.Path = directoryInfo.FullName;
            fileWatcher.IncludeSubdirectories = includeSubdirectories;
            fileWatcher.Filter =  filter;
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

        static FastScriptReloadManager()
        {
            //do not add init code in here as with domain reload turned off it won't be properly set on play-mode enter, use Init method instead
            EditorApplication.update += Instance.Update;
            EditorApplication.playModeStateChanged += Instance.OnEditorApplicationOnplayModeStateChanged;
        }
    
        [MenuItem("Assets/Fast Script Reload/Add Hot-Reload Exclusion", false)]
        public static void AddFileAsExcluded()
        {
            FastScriptReloadPreference.FilesExcludedFromHotReload.AddElement(ResolveRelativeToAssetDirectoryFilePath(Selection.activeObject));
        }
    
        [MenuItem("Assets/Fast Script Reload/Add Hot-Reload Exclusion", true)]
        public static bool AddFileAsExcludedValidateFn()
        {
            return Selection.activeObject is MonoScript
                   && !((FastScriptReloadPreference.FilesExcludedFromHotReload.GetEditorPersistedValueOrDefault() as IEnumerable<string>) ?? Array.Empty<string>())
                       .Contains(ResolveRelativeToAssetDirectoryFilePath(Selection.activeObject));
        }

        [MenuItem("Assets/Fast Script Reload/Remove Hot-Reload Exclusion", false)]
        public static void RemoveFileAsExcluded()
        {
            FastScriptReloadPreference.FilesExcludedFromHotReload.RemoveElement(ResolveRelativeToAssetDirectoryFilePath(Selection.activeObject));
        }
    
        [MenuItem("Assets/Fast Script Reload/Remove Hot-Reload Exclusion", true)]
        public static bool RemoveFileAsExcludedValidateFn()
        {
            return Selection.activeObject is MonoScript
                   && ((FastScriptReloadPreference.FilesExcludedFromHotReload.GetEditorPersistedValueOrDefault() as IEnumerable<string>) ?? Array.Empty<string>())
                   .Contains(ResolveRelativeToAssetDirectoryFilePath(Selection.activeObject));
        }
    
        [MenuItem("Assets/Fast Script Reload/Show Exclusions", false)]
        public static void ShowExcludedFilesInUi()
        {
            var window = FastScriptReloadWelcomeScreen.Init();
            window.OpenExclusionsSection();
        }
    
        private static string ResolveRelativeToAssetDirectoryFilePath(UnityEngine.Object obj)
        {
            return AssetDatabase.GetAssetPath(obj.GetInstanceID());
        }

        private void Update()
        {
            if (_executeOnlyInPlaymode && !EditorApplication.isPlaying)
            {
                return;
            }

            AssignConfigValuesThatCanNotBeAccessedOutsideOfMainThread();

            if (!_assemblyChangesLoaderResolverResolutionAlreadyCalled)
            {
                AssemblyChangesLoaderResolver.Instance.Resolve(); //WARN: need to resolve initially in case monobehaviour singleton is not created
                _assemblyChangesLoaderResolverResolutionAlreadyCalled = true;
            }

            if ((bool)FastScriptReloadPreference.EnableAutoReloadForChangedFiles.GetEditorPersistedValueOrDefault() &&
                (DateTime.UtcNow - _lastTimeChangeBatchRun).TotalSeconds > (int)FastScriptReloadPreference.BatchScriptChangesAndReloadEveryNSeconds.GetEditorPersistedValueOrDefault())
            {
                TriggerReloadForChangedFiles();
            }
        }

        private void AssignConfigValuesThatCanNotBeAccessedOutsideOfMainThread()
        {
            //TODO: PERF: needed in file watcher but when run on non-main thread causes exception. 
            _currentFileExclusions = FastScriptReloadPreference.FilesExcludedFromHotReload.GetElements();
            EnableExperimentalThisCallLimitationFix = (bool)FastScriptReloadPreference.EnableExperimentalThisCallLimitationFix.GetEditorPersistedValueOrDefault();
            AssemblyChangesLoaderEditorOptionsNeededInBuild.UpdateValues(
                (bool)FastScriptReloadPreference.IsDidFieldsOrPropertyCountChangedCheckDisabled.GetEditorPersistedValueOrDefault(),
                (bool)FastScriptReloadPreference.EnableExperimentalAddedFieldsSupport.GetEditorPersistedValueOrDefault()
            );
        }

        public void TriggerReloadForChangedFiles()
        {
            var assemblyChangesLoader = AssemblyChangesLoaderResolver.Instance.Resolve();
            var changesAwaitingHotReload = _dynamicFileHotReloadStateEntries
                .Where(e => e.IsAwaitingCompilation)
                .ToList();

            if (changesAwaitingHotReload.Any())
            {
                changesAwaitingHotReload.ForEach(c => { c.IsBeingProcessed = true; });

                UnityMainThreadDispatcher.Instance.EnsureInitialized();
                Task.Run(() =>
                {
                    List<string> sourceCodeFilesWithUniqueChangesAwaitingHotReload = null;
                    try
                    {
                        sourceCodeFilesWithUniqueChangesAwaitingHotReload = changesAwaitingHotReload
                            .GroupBy(e => e.FullFileName)
                            .Select(e => e.First().FullFileName).ToList();
                    
                        var dynamicallyLoadedAssemblyCompilerResult = DynamicAssemblyCompiler.Compile(sourceCodeFilesWithUniqueChangesAwaitingHotReload);
                        if (!dynamicallyLoadedAssemblyCompilerResult.IsError)
                        {
                            changesAwaitingHotReload.ForEach(c =>
                            {
                                c.FileCompiledOn = DateTime.UtcNow;
                                c.AssemblyNameCompiledIn = dynamicallyLoadedAssemblyCompilerResult.CompiledAssemblyPath;
                            });

                            //TODO: return some proper results to make sure entries are correctly updated
                            assemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(dynamicallyLoadedAssemblyCompilerResult.CompiledAssembly, AssemblyChangesLoaderEditorOptionsNeededInBuild);
                            changesAwaitingHotReload.ForEach(c =>
                            {
                                c.HotSwappedOn = DateTime.UtcNow;
                                c.IsBeingProcessed = false;
                            }); //TODO: technically not all were hot swapped at same time
                        }
                        else
                        {
                            if (dynamicallyLoadedAssemblyCompilerResult.MessagesFromCompilerProcess.Count > 0)
                            {
                                var msg = new StringBuilder();
                                foreach (string message in dynamicallyLoadedAssemblyCompilerResult.MessagesFromCompilerProcess)
                                {
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
                        Debug.LogError($"Error when updating files: '{(sourceCodeFilesWithUniqueChangesAwaitingHotReload != null ? string.Join(",", sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(fn => new FileInfo(fn).Name)) : "unknown")}', {ex}");
                    }
                });
            }

            _lastTimeChangeBatchRun = DateTime.UtcNow;
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

            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                Init();
            }
        }

        private static void Init()
        {
            if (!(bool)FastScriptReloadPreference.EnableAutoReloadForChangedFiles.GetEditorPersistedValueOrDefault())
            {
                LoggerScoped.LogDebug("Hot reload disabled, file watchers will not be initialized.");
                return;
            }
            
            if (Instance._fileWatchers.Count == 0)
            {
                var fileWatcherSetupEntries = FastScriptReloadPreference.FileWatcherSetupEntries.GetElementsTyped();
                if (fileWatcherSetupEntries.Count == 0)
                {
                    Debug.LogWarning($"FastScriptReload: There are no file watcher setup entries. Tool will not be able to pick changes automatically");
                }
                
                foreach (var fileWatcherSetupEntry in fileWatcherSetupEntries)
                {
                    Instance.StartWatchingDirectoryAndSubdirectories(fileWatcherSetupEntry.path, fileWatcherSetupEntry.filter, fileWatcherSetupEntry.includeSubdirectories);
                }
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
}