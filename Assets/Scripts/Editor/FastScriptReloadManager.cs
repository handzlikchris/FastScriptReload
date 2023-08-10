using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastScriptReload.Editor.Compilation;
using FastScriptReload.Editor.Compilation.ScriptGenerationOverrides;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Editor.Common.Utilities;
using ImmersiveVRTools.Runtime.Common;
using ImmersiveVrToolsCommon.Runtime.Logging;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FastScriptReload.Editor
{
    [InitializeOnLoad]
    [PreventHotReload]
    public class FastScriptReloadManager
    {
        private static FastScriptReloadManager _instance;
        public static FastScriptReloadManager Instance
        {
            get {
                if (_instance == null)
                {
                    _instance = new FastScriptReloadManager();
                    LoggerScoped.LogDebug("Created Manager");
                }

                return _instance;
            }
        }

        private static string DataPath = Application.dataPath;
        

        public const string FileWatcherReplacementTokenForApplicationDataPath = "<Application.dataPath>";
        private const int BaseMenuItemPriority_ManualScriptOverride = 100;
        private const int BaseMenuItemPriority_Exclusions = 200;
        private const int BaseMenuItemPriority_FileWatcher = 300;
        
        public Dictionary<string, Func<string>> FileWatcherTokensToResolvePathFn = new Dictionary<string, Func<string>>
        {
            [FileWatcherReplacementTokenForApplicationDataPath] = () => DataPath
        };
        
        private Dictionary<string, DynamicFileHotReloadState> _lastProcessedDynamicFileHotReloadStatesInSession = new Dictionary<string, DynamicFileHotReloadState>();
        public IReadOnlyDictionary<string, DynamicFileHotReloadState> LastProcessedDynamicFileHotReloadStatesInSession => _lastProcessedDynamicFileHotReloadStatesInSession;
        public event Action<List<DynamicFileHotReloadState>> HotReloadFailed;
        public event Action<List<DynamicFileHotReloadState>> HotReloadSucceeded;

        private bool _wasLockReloadAssembliesCalled;
        private PlayModeStateChange _lastPlayModeStateChange;
        private List<FileSystemWatcher> _fileWatchers = new List<FileSystemWatcher>();
        private IEnumerable<string> _currentFileExclusions;
        private int _triggerDomainReloadIfOverNDynamicallyLoadedAssembles = 100;
        public bool EnableExperimentalThisCallLimitationFix { get; private set; }
#pragma warning disable 0618
        public AssemblyChangesLoaderEditorOptionsNeededInBuild AssemblyChangesLoaderEditorOptionsNeededInBuild { get; private set; } = new AssemblyChangesLoaderEditorOptionsNeededInBuild();

#pragma warning restore 0618

        private List<DynamicFileHotReloadState> _dynamicFileHotReloadStateEntries = new List<DynamicFileHotReloadState>();

        private DateTime _lastTimeChangeBatchRun = default(DateTime);
        private bool _assemblyChangesLoaderResolverResolutionAlreadyCalled;
        private bool _isEditorModeHotReloadEnabled;
        private int _hotReloadPerformedCount = 0;
        private bool _isOnDemandHotReloadEnabled;

        private void OnWatchedFileChange(object source, FileSystemEventArgs e)
        {
            if (ShouldIgnoreFileChange()) return;

            var filePathToUse = e.FullPath;
            if (!File.Exists(filePathToUse))
            {
                if (!TryWorkaroundForUnityFileWatcherBug(e, ref filePathToUse)) 
                    return;
            }
            
            AddFileChangeToProcess(filePathToUse);
        }

        public void AddFileChangeToProcess(string filePath)
        {
            if (!File.Exists(filePath))
            {
                LoggerScoped.LogWarning($"Specified file: '{filePath}' does not exist. Hot-Reload will not be performed.");
                return;
            }
            
            if (_currentFileExclusions != null && _currentFileExclusions.Any(fp => filePath.Replace("\\", "/").EndsWith(fp)))
            {
                LoggerScoped.LogWarning($"FastScriptReload: File: '{filePath}' changed, but marked as exclusion. Hot-Reload will not be performed. You can manage exclusions via" +
                                        $"\r\nRight click context menu (Fast Script Reload > Add / Remove Hot-Reload exclusion)" +
                                        $"\r\nor via Window -> Fast Script Reload -> Start Screen -> Exclusion menu");
            
                return;
            }
            
            const int msThresholdToConsiderSameChangeFromDifferentFileWatchers = 500;
            var isDuplicatedChangesComingFromDifferentFileWatcher = _dynamicFileHotReloadStateEntries
                .Any(f => f.FullFileName == filePath
                          && (DateTime.UtcNow - f.FileChangedOn).TotalMilliseconds < msThresholdToConsiderSameChangeFromDifferentFileWatchers);
            if (isDuplicatedChangesComingFromDifferentFileWatcher)
            {
                LoggerScoped.LogWarning($"FastScriptReload: Looks like change to: {filePath} have already been added for processing. This can happen if you have multiple file watchers set in a way that they overlap.");
                return;
            }
            
            _dynamicFileHotReloadStateEntries.Add(new DynamicFileHotReloadState(filePath, DateTime.UtcNow));
        }

        public bool ShouldIgnoreFileChange()
        {
            if (!_isEditorModeHotReloadEnabled && _lastPlayModeStateChange != PlayModeStateChange.EnteredPlayMode)
            {
#if ImmersiveVrTools_DebugEnabled
            LoggerScoped.Log($"Application not playing, change to: {e.Name} won't be compiled and hot reloaded");
#endif
                return true;
            }

            return false;
        }

        private void StartWatchingDirectoryAndSubdirectories(string directoryPath, string filter, bool includeSubdirectories) 
        {
            foreach (var kv in FileWatcherTokensToResolvePathFn)
            {
                directoryPath = directoryPath.Replace(kv.Key, kv.Value());
            }
            
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (!directoryInfo.Exists)
            {
                LoggerScoped.LogWarning($"FastScriptReload: Directory: '{directoryPath}' does not exist, make sure file-watcher setup is correct. You can access via: Window -> Fast Script Reload -> File Watcher (Advanced Setup)");
            }
            
            var isUsingCustomFileWatcher = (bool)FastScriptReloadPreference.EnableCustomFileWatcher.GetEditorPersistedValueOrDefault();
            if (isUsingCustomFileWatcher)
            {
                CustomFileWatcher.InitializeSingularFilewatcher(directoryPath, filter, includeSubdirectories);
            }
            else
            {
                var fileWatcher = new FileSystemWatcher();

                fileWatcher.Path = directoryInfo.FullName;
                fileWatcher.IncludeSubdirectories = includeSubdirectories;
                fileWatcher.Filter =  filter;
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.Changed += OnWatchedFileChange;
        
                fileWatcher.EnableRaisingEvents = true;
        
                _fileWatchers.Add(fileWatcher);
            }
        }

        static FastScriptReloadManager()
        {
            //do not add init code in here as with domain reload turned off it won't be properly set on play-mode enter, use Init method instead
            EditorApplication.update += Instance.Update;
            EditorApplication.playModeStateChanged += Instance.OnEditorApplicationOnplayModeStateChanged;

            ///if <see cref="FastScriptReloadPreference.WatchOnlySpecified"/> is enabled, disable auto reload automatically when launching editor. Will be enabled automatically when adding file watcher manually
            if ((bool)FastScriptReloadPreference.WatchOnlySpecified.GetEditorPersistedValueOrDefault() && SessionState.GetBool("NEED_EDITOR_SESSION_INIT", true))
            {
                SessionState.SetBool("NEED_EDITOR_SESSION_INIT", false);
                ClearFileWatchersEntries();
            }
        }

        ~FastScriptReloadManager()
        {
            LoggerScoped.LogDebug("Destroying FSR Manager "); 
            if (_instance != null)
            {
                if (_lastPlayModeStateChange == PlayModeStateChange.EnteredPlayMode)
                {
                    LoggerScoped.LogError("Manager is being destroyed in play session, this indicates some sort of issue where static variables were reset, hot reload will not function properly please reset. " +
                                          "This is usually caused by Unity triggering that reset for some reason that's outside of asset control - other static variables will also be affected and recovering just hot reload would hide wider issue.");
                }
                ClearFileWatchers();
            }
        }

        private const string WatchSpecificFileOrFolderMenuItemName = "Assets/Fast Script Reload/Watch File\\Folder";
        [MenuItem(WatchSpecificFileOrFolderMenuItemName, true, BaseMenuItemPriority_FileWatcher + 1)]
        public static bool ToggleSelectionFileWatchersSetupValidation()
        {
            if (!(bool)FastScriptReloadPreference.WatchOnlySpecified.GetEditorPersistedValueOrDefault())
            {
                return false;
            }
            
            Menu.SetChecked(WatchSpecificFileOrFolderMenuItemName, false);

            var isSelectionContaininingFolderOrScript = false;
            for (var i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is MonoScript selectedMonoScript)
                {
                    isSelectionContaininingFolderOrScript = true;

                    if (IsFileWatcherSetupEntryAlreadyPresent(selectedMonoScript))
                    {
                        Menu.SetChecked(WatchSpecificFileOrFolderMenuItemName, true);
                        break;
                    }
                }
                else if (Selection.objects[i] is DefaultAsset selectedAsset)
                {
                    isSelectionContaininingFolderOrScript = true;

                    if (IsFileWatcherSetupEntryAlreadyPresent(selectedAsset))
                    {
                        Menu.SetChecked(WatchSpecificFileOrFolderMenuItemName, true);
                        break;
                    }
                }
            }

            return isSelectionContaininingFolderOrScript;
        }

        /// <summary>Used to add/remove scripts/folders to the <see cref="FastScriptReloadPreference.FileWatcherSetupEntries"/></summary>
        [MenuItem(WatchSpecificFileOrFolderMenuItemName, false, BaseMenuItemPriority_FileWatcher + 1)]
        public static void ToggleSelectionFileWatchersSetup()
        {
            var isFileWatchersChange = false;
            for (var i = 0; i < Selection.objects.Length; i++)
            {
                if (Selection.objects[i] is MonoScript selectedMonoScript)
                {
                    if (IsFileWatcherSetupEntryAlreadyPresent(selectedMonoScript, out var foundFileWatcherSetupEntry))
                    {
                        FastScriptReloadPreference.FileWatcherSetupEntries.RemoveElement(JsonUtility.ToJson(foundFileWatcherSetupEntry));
                    }
                    else
                    {
                        FastScriptReloadPreference.FileWatcherSetupEntries.AddElement(JsonUtility.ToJson(foundFileWatcherSetupEntry));
                    }
                    
                    isFileWatchersChange = true;
                }
                else if (Selection.objects[i] is DefaultAsset selectedAsset)
                {
                    if (IsFileWatcherSetupEntryAlreadyPresent(selectedAsset, out var foundFileWatcherSetupEntry))
                    {
                        FastScriptReloadPreference.FileWatcherSetupEntries.RemoveElement(JsonUtility.ToJson(foundFileWatcherSetupEntry));
                    }
                    else
                    {
                        FastScriptReloadPreference.FileWatcherSetupEntries.AddElement(JsonUtility.ToJson(foundFileWatcherSetupEntry));
                    }
                    
                    isFileWatchersChange = true;
                }
            }

            if (isFileWatchersChange)
            {
                FastScriptReloadPreference.FileWatcherSetupEntriesChanged = true; // Ensures file watcher are updated in play mode

                /// When in <see cref="FastScriptReloadPreference.WatchOnlySpecified"/> mode, <see cref="FastScriptReloadPreference.EnableAutoReloadForChangedFiles"/> state is managed automatically (disabled when no file watcher)
                if ((bool)FastScriptReloadPreference.WatchOnlySpecified.GetEditorPersistedValueOrDefault())
                {
                    var isAnyFileWatcherSet = FastScriptReloadPreference.FileWatcherSetupEntries.GetElements().Any();
                    FastScriptReloadPreference.EnableAutoReloadForChangedFiles.SetEditorPersistedValue(isAnyFileWatcherSet);
                }
            }
        }

        [MenuItem("Assets/Fast Script Reload/Clear Watched Files", true, BaseMenuItemPriority_FileWatcher + 2)]
        public static bool ClearFastScriptReloadValidation()
        {
            if (!(bool)FastScriptReloadPreference.WatchOnlySpecified.GetEditorPersistedValueOrDefault())
            {
                return false;
            }

            return FastScriptReloadPreference.FileWatcherSetupEntries.GetElements().Any();
        }
        [MenuItem("Assets/Fast Script Reload/Clear Watched Files", false, BaseMenuItemPriority_FileWatcher + 2)]
        public static void ClearFileWatchersEntries()
        {
            foreach (var item in FastScriptReloadPreference.FileWatcherSetupEntries.GetElements())
            {
                FastScriptReloadPreference.FileWatcherSetupEntries.RemoveElement(item);
            }
            Debug.LogWarning("File Watcher Setup has been cleared - make sure to add some.");

            FastScriptReloadPreference.EnableAutoReloadForChangedFiles.SetEditorPersistedValue(false);

            ClearFileWatchers();
        }


        [MenuItem("Assets/Fast Script Reload/Add \\ Open User Script Rewrite Override", false, BaseMenuItemPriority_ManualScriptOverride + 1)]
        public static void AddHotReloadManualScriptOverride()
        {
            if (Selection.activeObject is MonoScript script)
            {
                ScriptGenerationOverridesManager.AddScriptOverride(script);
            }
        }
        
        [MenuItem("Assets/Fast Script Reload/Add \\ Open User Script Rewrite Override", true)]
        public static bool AddHotReloadManualScriptOverrideValidateFn()
        {
            return Selection.activeObject is MonoScript;
        }
        
        [MenuItem("Assets/Fast Script Reload/Remove User Script Rewrite Override", false, BaseMenuItemPriority_ManualScriptOverride + 2)]
        public static void RemoveHotReloadManualScriptOverride()
        {
            if (Selection.activeObject is MonoScript script)
            {
                ScriptGenerationOverridesManager.TryRemoveScriptOverride(script);
            }
        }
        
        [MenuItem("Assets/Fast Script Reload/Remove User Script Rewrite Override", true)]
        public static bool RemoveHotReloadManualScriptOverrideValidateFn()
        {
            if (Selection.activeObject is MonoScript script)
            {
                return ScriptGenerationOverridesManager.TryGetScriptOverride(  
                    new FileInfo(Path.Combine(Path.Combine(Application.dataPath + "//..", AssetDatabase.GetAssetPath(script)))),
                    out var _
                );
            }

            return false;
        }
        
        [MenuItem("Assets/Fast Script Reload/Show User Script Rewrite Overrides", false, BaseMenuItemPriority_ManualScriptOverride + 3)]
        public static void ShowManualScriptRewriteOverridesInUi()
        {
            var window = FastScriptReloadWelcomeScreen.Init();
            window.OpenUserScriptRewriteOverridesSection();
        }
        
        [MenuItem("Assets/Fast Script Reload/Add Hot-Reload Exclusion", false, BaseMenuItemPriority_Exclusions + 1)]
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

        [MenuItem("Assets/Fast Script Reload/Remove Hot-Reload Exclusion", false, BaseMenuItemPriority_Exclusions + 2)]
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
    
        [MenuItem("Assets/Fast Script Reload/Show Exclusions", false, BaseMenuItemPriority_Exclusions + 3)]
        public static void ShowExcludedFilesInUi()
        {
            var window = FastScriptReloadWelcomeScreen.Init();
            window.OpenExclusionsSection();
        }
        
        private static string ResolveRelativeToAssetDirectoryFilePath(UnityEngine.Object obj)
        {
            return AssetDatabase.GetAssetPath(obj.GetInstanceID());
        }

        public void Update()
        {
            _isEditorModeHotReloadEnabled = (bool)FastScriptReloadPreference.EnableExperimentalEditorHotReloadSupport.GetEditorPersistedValueOrDefault();
            if (_lastPlayModeStateChange == PlayModeStateChange.ExitingPlayMode && Instance._fileWatchers.Any())
            {
                ClearFileWatchers();
            }
            
            if (!_isEditorModeHotReloadEnabled && !EditorApplication.isPlaying)
            {
                return;
            }

            if (_isEditorModeHotReloadEnabled)
            {
                EnsureInitialized();
            }
            else if (_lastPlayModeStateChange == PlayModeStateChange.EnteredPlayMode)
            {

                EnsureInitialized();

                // if (_lastPlayModeStateChange != PlayModeStateChange.ExitingPlayMode && Application.isPlaying && Instance._fileWatchers.Count == 0 && FastScriptReloadPreference.FileWatcherSetupEntries.GetElementsTyped().Count > 0)
                // {
                //     LoggerScoped.LogWarning("Reinitializing file-watchers as defined configuration does not match current instance setup. If hot reload still doesn't work you'll need to reset play session.");
                //     ClearFileWatchers();
                //     EnsureInitialized();
                // }
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
        
        private static void ClearFileWatchers()
        {
            foreach (var fileWatcher in Instance._fileWatchers)
            {
                fileWatcher.Dispose();
            }

            Instance._fileWatchers.Clear();
        }

        private void AssignConfigValuesThatCanNotBeAccessedOutsideOfMainThread()
        {
            //TODO: PERF: needed in file watcher but when run on non-main thread causes exception. 
            _currentFileExclusions = FastScriptReloadPreference.FilesExcludedFromHotReload.GetElements();
            _triggerDomainReloadIfOverNDynamicallyLoadedAssembles = (int)FastScriptReloadPreference.TriggerDomainReloadIfOverNDynamicallyLoadedAssembles.GetEditorPersistedValueOrDefault();
            _isOnDemandHotReloadEnabled = (bool)FastScriptReloadPreference.EnableOnDemandReload.GetEditorPersistedValueOrDefault();
            EnableExperimentalThisCallLimitationFix = (bool)FastScriptReloadPreference.EnableExperimentalThisCallLimitationFix.GetEditorPersistedValueOrDefault();
            AssemblyChangesLoaderEditorOptionsNeededInBuild.UpdateValues(
                (bool)FastScriptReloadPreference.IsDidFieldsOrPropertyCountChangedCheckDisabled.GetEditorPersistedValueOrDefault(),
                (bool)FastScriptReloadPreference.EnableExperimentalAddedFieldsSupport.GetEditorPersistedValueOrDefault()
            );
        }

        public void TriggerReloadForChangedFiles()
        {
            if (!Application.isPlaying && _hotReloadPerformedCount > _triggerDomainReloadIfOverNDynamicallyLoadedAssembles)
            {
                _hotReloadPerformedCount = 0;
                LoggerScoped.LogWarning($"Dynamically created assembles reached over: {_triggerDomainReloadIfOverNDynamicallyLoadedAssembles} - triggering full domain reload to clean up. You can adjust that value in settings.");
#if UNITY_2019_3_OR_NEWER
                CompilationPipeline.RequestScriptCompilation(); //TODO: add some timer to ensure this does not go into some kind of loop
#elif UNITY_2017_1_OR_NEWER
                 var editorAssembly = Assembly.GetAssembly(typeof(Editor));
                 var editorCompilationInterfaceType = editorAssembly.GetType("UnityEditor.Scripting.ScriptCompilation.EditorCompilationInterface");
                 var dirtyAllScriptsMethod = editorCompilationInterfaceType.GetMethod("DirtyAllScripts", BindingFlags.Static | BindingFlags.Public);
                 dirtyAllScriptsMethod.Invoke(editorCompilationInterfaceType, null);
#endif
                ClearLastProcessedDynamicFileHotReloadStates();
            }
            
            var assemblyChangesLoader = AssemblyChangesLoaderResolver.Instance.Resolve();
            var changesAwaitingHotReload = _dynamicFileHotReloadStateEntries
                .Where(e => e.IsAwaitingCompilation)
                .ToList();

            if (changesAwaitingHotReload.Any())
            {
                UpdateLastProcessedDynamicFileHotReloadStates(changesAwaitingHotReload);
                foreach (var c in changesAwaitingHotReload)
                {
                    c.IsBeingProcessed = true;
                }

                var unityMainThreadDispatcher = UnityMainThreadDispatcher.Instance.EnsureInitialized(); //need to pass that in, resolving on other than main thread will cause exception
                Task.Run(() =>
                {
                    List<string> sourceCodeFilesWithUniqueChangesAwaitingHotReload = null;
                    try
                    {
                        sourceCodeFilesWithUniqueChangesAwaitingHotReload = changesAwaitingHotReload
                            .GroupBy(e => e.FullFileName)
                            .Select(e => e.First().FullFileName).ToList();
                    
                        var dynamicallyLoadedAssemblyCompilerResult = DynamicAssemblyCompiler.Compile(sourceCodeFilesWithUniqueChangesAwaitingHotReload, unityMainThreadDispatcher);
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

                            _hotReloadPerformedCount++;
                            
                            SafeInvoke(HotReloadSucceeded, changesAwaitingHotReload);
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
                        if (ex is SourceCodeHasErrorsException e)
                            LoggerScoped.LogError(e.Message + Environment.NewLine);
                        else
                            LoggerScoped.LogError($"Error when updating files: '{(sourceCodeFilesWithUniqueChangesAwaitingHotReload != null ? string.Join(",", sourceCodeFilesWithUniqueChangesAwaitingHotReload.Select(fn => new FileInfo(fn).Name)) : "unknown")}', {ex}");
                        
                        changesAwaitingHotReload.ForEach(c =>
                        {
                            c.ErrorOn = DateTime.UtcNow;
                            c.ErrorText = ex.Message;
                            c.SourceCodeCombinedFilePath = (ex as HotReloadCompilationException)?.SourceCodeCombinedFileCreated;
                        });

                        SafeInvoke(HotReloadFailed, changesAwaitingHotReload);
                    }
                });
            }

            _lastTimeChangeBatchRun = DateTime.UtcNow;
        }

        private void SafeInvoke(Action<List<DynamicFileHotReloadState>> ev, List<DynamicFileHotReloadState> changesAwaitingHotReload)
        {
            try
            {
                ev?.Invoke(changesAwaitingHotReload);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error when executing event, {e}");
            }
        }

        private void AddToLastProcessedDynamicFileHotReloadStates(DynamicFileHotReloadState c)
        {
            var assetGuid = AssetDatabaseHelper.AbsolutePathToGUID(c.FullFileName);
            if (!string.IsNullOrEmpty(assetGuid))
            {
                _lastProcessedDynamicFileHotReloadStatesInSession[assetGuid] = c;
            }
        }
        
        private void ClearLastProcessedDynamicFileHotReloadStates()
        {
            _lastProcessedDynamicFileHotReloadStatesInSession.Clear();
        }
        
        //Success entries will always be cleared - errors will remain till another change fixes them
        private void UpdateLastProcessedDynamicFileHotReloadStates(List<DynamicFileHotReloadState> changesToHotReload)
        {
            var succeededReloads = _lastProcessedDynamicFileHotReloadStatesInSession
                .Where(s => s.Value.IsChangeHotSwapped).ToList();
            foreach (var kv in succeededReloads)
            {
                _lastProcessedDynamicFileHotReloadStatesInSession.Remove(kv.Key);
            }

            foreach (var changeToHotReload in changesToHotReload)
            {
                AddToLastProcessedDynamicFileHotReloadStates(changeToHotReload);
            }
        }

        private void OnEditorApplicationOnplayModeStateChanged(PlayModeStateChange obj)
        {
            Instance._lastPlayModeStateChange = obj;

            if ((bool)FastScriptReloadPreference.IsForceLockAssembliesViaCode.GetEditorPersistedValueOrDefault())
            {
                if (obj == PlayModeStateChange.EnteredPlayMode)
                {
                    EditorApplication.LockReloadAssemblies();
                    _wasLockReloadAssembliesCalled = true;
                }
            }
            
            if(obj == PlayModeStateChange.EnteredEditMode && _wasLockReloadAssembliesCalled)
            {
                EditorApplication.UnlockReloadAssemblies();
                _wasLockReloadAssembliesCalled = false;
            }
        }
        
                private static bool TryWorkaroundForUnityFileWatcherBug(FileSystemEventArgs e, ref string filePathToUse)
        {
            LoggerScoped.LogWarning(@"Fast Script Reload - Unity File Path Bug - Warning!
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
                LoggerScoped.LogError($"FileWatcherBugWorkaround: Unable to find file '{changedFileName}', changes will not be reloaded. Please update unity editor.");
                return false;
            }
            else if (fileFoundInAssets.Length == 1)
            {
                LoggerScoped.Log($"FileWatcherBugWorkaround: Original Unity passed file path: '{e.FullPath}' adjusted to found: '{fileFoundInAssets[0]}'");
                filePathToUse = fileFoundInAssets[0];
                return true;
            }
            else
            {
                LoggerScoped.LogWarning($"FileWatcherBugWorkaround: Multiple files found. Original Unity passed file path: '{e.FullPath}' adjusted to found: '{fileFoundInAssets[0]}'");
                filePathToUse = fileFoundInAssets[0];
                return true;
            }
        }

        private static bool HotReloadDisabled_WarningMessageShownAlready;
        private static void EnsureInitialized()
        {
            if (!(bool)FastScriptReloadPreference.EnableAutoReloadForChangedFiles.GetEditorPersistedValueOrDefault()
                && !(bool)FastScriptReloadPreference.EnableOnDemandReload.GetEditorPersistedValueOrDefault()
                && !(bool)FastScriptReloadPreference.WatchOnlySpecified.GetEditorPersistedValueOrDefault())
            {
                if (!HotReloadDisabled_WarningMessageShownAlready)
                {
                    LoggerScoped.LogWarning($"Neither auto hot reload / on-demand reload / or watch specific is specified, file watchers will not be initialized. Please adjust settings and restart if you want hot reload to work.");
                    HotReloadDisabled_WarningMessageShownAlready = true;
                }
                return;
            }
            
            var isUsingCustomFileWatchers = (bool)FastScriptReloadPreference.EnableCustomFileWatcher.GetEditorPersistedValueOrDefault();
            if (!isUsingCustomFileWatchers)
            {
                if (Instance._fileWatchers.Count == 0 || FastScriptReloadPreference.FileWatcherSetupEntriesChanged)
                {
                    FastScriptReloadPreference.FileWatcherSetupEntriesChanged = false;

                    InitializeFromFileWatcherSetupEntries();
                }
            }
            else if(!CustomFileWatcher.InitSignaled)
            {
                CustomFileWatcher.TryEnableLivewatching();
                InitializeFromFileWatcherSetupEntries();
                CustomFileWatcher.InitSignaled = true;
            }
        }

        private static void InitializeFromFileWatcherSetupEntries()
        {
            var fileWatcherSetupEntries = FastScriptReloadPreference.FileWatcherSetupEntries.GetElementsTyped();
            if (fileWatcherSetupEntries.Count == 0)
            {
                LoggerScoped.LogWarning($"There are no file watcher setup entries. Tool will not be able to pick changes automatically");
            }

            foreach (var fileWatcherSetupEntry in fileWatcherSetupEntries)
            {
                Instance.StartWatchingDirectoryAndSubdirectories(
                    fileWatcherSetupEntry.path,
                    fileWatcherSetupEntry.filter,
                    fileWatcherSetupEntry.includeSubdirectories
                );
            }
        }

        private static bool IsFileWatcherSetupEntryAlreadyPresent(FileWatcherSetupEntry fileWatcherSetupEntry)
        {
            //TODO: could be a bit of a per hit, GetElementsTypes will parse json every time
            return FastScriptReloadPreference.FileWatcherSetupEntries.GetElementsTyped()
                .Any(e => e.path == fileWatcherSetupEntry.path 
                          && e.filter == fileWatcherSetupEntry.filter 
                          && e.includeSubdirectories == fileWatcherSetupEntry.includeSubdirectories);
        }

        private static bool IsFileWatcherSetupEntryAlreadyPresent(DefaultAsset selectedAsset)
        {
            FileWatcherSetupEntry fileWatcherSetupEntry;
            return IsFileWatcherSetupEntryAlreadyPresent(selectedAsset, out fileWatcherSetupEntry);
        }
        
        private static bool IsFileWatcherSetupEntryAlreadyPresent(DefaultAsset selectedAsset, out FileWatcherSetupEntry fileWatcherSetupEntry)
        {
            var path = FileWatcherReplacementTokenForApplicationDataPath + AssetDatabase.GetAssetPath(selectedAsset).Remove(0, "Assets".Length);
            fileWatcherSetupEntry = new FileWatcherSetupEntry(path, "*.cs", true);

            var isFileWatcherSetupEntryAlreadyPresent = IsFileWatcherSetupEntryAlreadyPresent(fileWatcherSetupEntry);
            return isFileWatcherSetupEntryAlreadyPresent;
        }

        private static bool IsFileWatcherSetupEntryAlreadyPresent(MonoScript selectedMonoScript)
        {
            FileWatcherSetupEntry fileWatcherSetupEntry;
            return IsFileWatcherSetupEntryAlreadyPresent(selectedMonoScript, out fileWatcherSetupEntry);
        }

        private static bool IsFileWatcherSetupEntryAlreadyPresent(MonoScript selectedMonoScript, out FileWatcherSetupEntry fileWatcherSetupEntry)
        {
            var path = FileWatcherReplacementTokenForApplicationDataPath + AssetDatabase.GetAssetPath(selectedMonoScript).Remove(0, "Assets".Length);
            var fileSeperatorIndex = path.LastIndexOf('/');
            var fileName = path.Substring(fileSeperatorIndex + 1);
            path = path.Substring(0, fileSeperatorIndex);

            fileWatcherSetupEntry = new FileWatcherSetupEntry(path, fileName, false);
            var isFileWatcherSetupEntryAlreadyPresent = IsFileWatcherSetupEntryAlreadyPresent(fileWatcherSetupEntry);
            return isFileWatcherSetupEntryAlreadyPresent;
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
        public bool IsChangeHotSwapped => HotSwappedOn.HasValue;
    
        public string ErrorText { get; set; }
        public DateTime? ErrorOn { get; set; }
        public bool IsFailed => ErrorOn.HasValue;
        public bool IsBeingProcessed { get; set; }
        public string SourceCodeCombinedFilePath { get; set; }

        public DynamicFileHotReloadState(string fullFileName, DateTime fileChangedOn)
        {
            FullFileName = fullFileName;
            FileChangedOn = fileChangedOn;
        }
    }
}