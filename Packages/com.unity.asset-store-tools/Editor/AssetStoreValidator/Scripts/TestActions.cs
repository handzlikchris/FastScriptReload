using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using CompilationPipeline = UnityEditor.Compilation.CompilationPipeline;
using Object = UnityEngine.Object;

namespace AssetStoreTools.Validator
{
    internal class TestActions
    {
        private enum FileType
        {
            Prefab,
            Model,
            Scene,
            UnityPackage,
            JPG,
            Documentation,
            JavaScript,
            LossyAudio,
            NonLossyAudio,
            Video,
            Executable,
            Mixamo,
            SpeedTree,
            Texture,
            Shader,
            MonoScript,
            PrecompiledAssembly
        }

        public static TestActions Instance => s_instance ?? (s_instance = new TestActions());

        private static TestActions s_instance;

        private static string s_mainFolderPath;

        private TestActions() { }

        public void SetMainPath(string path)
        {
            s_mainFolderPath = path;
        }

        #region HelperMethods

        private UnityEngine.Object[] GetObjectsFromAssets(FileType type)
        {
            string[] guids = null;
            string[] extensions = null;

            switch (type)
            {
                // General Types
                case FileType.Prefab:
                    guids = AssetDatabase.FindAssets("t:prefab", new[] { s_mainFolderPath });
                    break;
                case FileType.Model:
                    guids = AssetDatabase.FindAssets("t:model", new[] { s_mainFolderPath });
                    break;
                case FileType.Scene:
                    guids = AssetDatabase.FindAssets("t:scene", new[] { s_mainFolderPath });
                    break;
                case FileType.Texture:
                    guids = AssetDatabase.FindAssets("t:texture", new[] { s_mainFolderPath });
                    break;
                case FileType.Video:
                    guids = AssetDatabase.FindAssets("t:VideoClip", new[] { s_mainFolderPath });
                    break;
                // Specific Types
                case FileType.UnityPackage:
                    guids = AssetDatabase.FindAssets("", new[] { s_mainFolderPath });
                    extensions = new[] { ".unitypackage" };
                    break;
                case FileType.LossyAudio:
                    guids = AssetDatabase.FindAssets("t:AudioClip", new[] { s_mainFolderPath });
                    extensions = new[] { ".mp3", ".ogg" };
                    break;
                case FileType.NonLossyAudio:
                    guids = AssetDatabase.FindAssets("t:AudioClip", new[] { s_mainFolderPath });
                    extensions = new[] { ".wav", ".aif", ".aiff"};
                    break;
                case FileType.JavaScript:
                    guids = AssetDatabase.FindAssets("t:TextAsset", new[] { s_mainFolderPath });
                    extensions = new[] { ".js" };
                    break;
                case FileType.Mixamo:
                    guids = AssetDatabase.FindAssets("t:model", new[] { s_mainFolderPath });
                    extensions = new[] { ".fbx" };
                    break;
                case FileType.JPG:
                    guids = AssetDatabase.FindAssets("t:texture", new[] { s_mainFolderPath });
                    extensions = new[] { ".jpg", "jpeg" };
                    break;
                case FileType.Executable:
                    guids = AssetDatabase.FindAssets("", new[] { s_mainFolderPath });
                    extensions = new[] { ".exe", ".bat", ".msi", ".apk" };
                    break;
                case FileType.Documentation:
                    guids = AssetDatabase.FindAssets("", new[] { s_mainFolderPath });
                    extensions = new[] { ".txt", ".pdf", ".html", ".rtf", ".md" };
                    break;
                case FileType.SpeedTree:
                    guids = AssetDatabase.FindAssets("", new[] { s_mainFolderPath });
                    extensions = new[] { ".spm", ".srt", ".stm", ".scs", ".sfc", ".sme", ".st" };
                    break;
                case FileType.Shader:
                    guids = AssetDatabase.FindAssets("", new[] { s_mainFolderPath });
                    extensions = new[] { ".shader", ".shadergraph", ".raytrace", ".compute" };
                    break;
                case FileType.MonoScript:
                    guids = AssetDatabase.FindAssets("t:script", new[] { s_mainFolderPath });
                    extensions = new[] { ".cs" };
                    break;
                case FileType.PrecompiledAssembly:
                    var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
                    var allDllPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly);
                    var dllPaths = allDllPaths.Select(x => x.StartsWith(rootProjectPath) ? x.Substring(rootProjectPath.Length) : x).Where(x => x.StartsWith(s_mainFolderPath)).ToArray();
                    return dllPaths.Select(x => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(x)).ToArray();
                default:
                    guids = Array.Empty<string>();
                    break;
            }

            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToArray();

            if (extensions != null)
                paths = paths.Where(x => extensions.Any(x.ToLower().EndsWith)).ToArray();

            if (type == FileType.Mixamo)
                paths = paths.Where(IsMixamoFbx).ToArray();

            paths = paths.Distinct().ToArray();

            var objects = paths.Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>).ToArray();
            return objects;
        }

        private static bool IsMixamoFbx(string fbxPath)
        {
            // Location of Mixamo Header, this is located in every mixamo fbx file exported
            //const int mixamoHeader = 0x4c0 + 2; // < this is the original location from A$ Tools, unsure if Mixamo file headers were changed since then
            const int mixamoHeader = 1622;
            // Length of Mixamo header
            const int length = 0xa;

            var fs = new FileStream(fbxPath, FileMode.Open);
            // Check if length is further than
            if (fs.Length < mixamoHeader)
                return false;

            byte[] buffer = new byte[length];
            using (BinaryReader reader = new BinaryReader(fs))
            {
                reader.BaseStream.Seek(mixamoHeader, SeekOrigin.Begin);
                reader.Read(buffer, 0, length);
            }

            string result = System.Text.Encoding.ASCII.GetString(buffer);
            return result.Contains("Mixamo");
        }

        private Mesh[] GetCustomMeshesInObject(GameObject obj)
        {
            var meshes = new List<Mesh>();

            var meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
            var skinnedMeshes = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            meshes.AddRange(meshFilters.Select(m => m.sharedMesh));
            meshes.AddRange(skinnedMeshes.Select(m => m.sharedMesh));

            meshes = meshes.Where(m => AssetDatabase.GetAssetPath(m).StartsWith("Assets/") ||
            AssetDatabase.GetAssetPath(m).StartsWith("Packages/")).ToList();

            return meshes.ToArray();
        }

        #endregion

        #region 1_IncludeDemoScene

        public TestResult _1_IncludeDemoScene()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var scenes = GetObjectsFromAssets(FileType.Scene);


            if (scenes.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("Could not find any Scenes in the selected folder.");
                result.AddMessage("Please make sure you have selected the correct main folder of your package " +
                                  "and it includes a Demo Scene.");

                return result;
            }

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var originalScenePath = SceneManager.GetActiveScene().path;
            var demoScenes = scenes.Where(x => CanBeDemoScene(AssetDatabase.GetAssetPath(x))).ToArray();

            if (string.IsNullOrEmpty(originalScenePath))
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
            else
                EditorSceneManager.OpenScene(originalScenePath);

            if (demoScenes.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("Could not find any Demo Scenes in the selected folder.");
            }
            else
            {
                result.AddMessage("Scenes found", null, demoScenes);
                result.AddMessage("If these Scenes should not belong to your package, " +
                                  "make sure you have selected the correct main folder.");
            }

            return result;
        }

        private bool CanBeDemoScene(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath);
            var rootObjects = SceneManager.GetSceneByPath(scenePath).GetRootGameObjects();
            var count = rootObjects.Length;

            if (count == 0)
                return false;

            if (count != 2)
                return true;

            var cameraGOUnchanged = rootObjects.Any(o => o.TryGetComponent<Camera>(out _) && o.GetComponents(typeof(Component)).Length == 3);
            var lightGOUnchanged = rootObjects.Any(o => o.TryGetComponent<Light>(out _) && o.GetComponents(typeof(Component)).Length == 2);

            return !cameraGOUnchanged || !lightGOUnchanged;
        }

        #endregion

        #region 2_MeshesHavePrefabs

        public TestResult _2_MeshesHavePrefabs()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var usedModelPaths = new List<string>();
            var prefabs = GetObjectsFromAssets(FileType.Prefab);
            var missingMeshReferencePrefabs = new List<GameObject>();

            // Get all meshes in existing prefabs and check if prefab has missing mesh references
            foreach (var o in prefabs)
            {
                var p = (GameObject)o;
                if (p == null)
                {
                    Debug.LogWarning($"Unable to load Prefab in {AssetDatabase.GetAssetPath(p)}");
                    continue;
                }

                Mesh[] meshes = GetCustomMeshesInObject(p);
                foreach (var mesh in meshes)
                {
                    string meshPath = AssetDatabase.GetAssetPath(mesh);
                    usedModelPaths.Add(meshPath);
                }

                if (HasMissingMeshReferences(p))
                    missingMeshReferencePrefabs.Add(p);
            }

            // Get all meshes in existing models
            List<string> allModelPaths = GetAllModelMeshPaths();

            // Get the list of meshes without prefabs
            List<string> unusedModels = allModelPaths.Except(usedModelPaths).ToList();

            if (unusedModels.Count == 0)
            {
                result.AddMessage("All found prefabs have meshes!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            var models = unusedModels.Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>).ToArray();
            result.AddMessage("The following models do not have associated prefabs", null, models);

            if (missingMeshReferencePrefabs.Count > 0)
                result.AddMessage("The following prefabs have missing mesh references", null, missingMeshReferencePrefabs.ToArray());

            return result;
        }

        private List<string> GetAllModelMeshPaths()
        {
            var models = GetObjectsFromAssets(FileType.Model);
            var paths = new List<string>();

            foreach (var o in models)
            {
                var m = (GameObject)o;
                var modelPath = AssetDatabase.GetAssetPath(m);
                var assetImporter = AssetImporter.GetAtPath(modelPath);
                if (assetImporter is ModelImporter modelImporter)
                {
                    var clips = modelImporter.clipAnimations.Count();
                    var meshes = GetCustomMeshesInObject(m);

                    // Only add if the model has meshes and no clips
                    if (meshes.Any() && clips == 0)
                        paths.Add(modelPath);
                }
            }

            return paths;
        }

        private bool HasMissingMeshReferences(GameObject go)
        {
            var meshes = go.GetComponentsInChildren<MeshFilter>(true);
            var skinnedMeshes = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (meshes.Length == 0 && skinnedMeshes.Length == 0)
                return false;

            if (meshes.Any(x => x.sharedMesh == null) || skinnedMeshes.Any(x => x.sharedMesh == null))
                return true;

            return false;
        }

        #endregion

        #region 3_ResetPrefabs

        public TestResult _3_ResetPrefabs()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var prefabs = GetObjectsFromAssets(FileType.Prefab);
            var badPrefabs = new List<GameObject>();
            var badPrefabsLowOffset = new List<GameObject>();

            foreach (var o in prefabs)
            {
                var p = (GameObject)o;
                var hasRectTransform = p.TryGetComponent(out RectTransform _);
                if (hasRectTransform || !GetCustomMeshesInObject(p).Any())
                    continue;

                var positionString = p.transform.position.ToString("F12");
                var rotationString = p.transform.rotation.eulerAngles.ToString("F12");
                var localScaleString = p.transform.localScale.ToString("F12");

                var vectorZeroString = Vector3.zero.ToString("F12");
                var vectorOneString = Vector3.one.ToString("F12");

                if (positionString != vectorZeroString || rotationString != vectorZeroString || localScaleString != vectorOneString)
                {
                    if (p.transform.position == Vector3.zero && p.transform.rotation.eulerAngles == Vector3.zero && p.transform.localScale == Vector3.one)
                        badPrefabsLowOffset.Add(p);
                    else
                        badPrefabs.Add(p);
                }
            }

            if (badPrefabs.Count == 0 && badPrefabsLowOffset.Count == 0)
            {
                result.AddMessage("All found prefabs were reset!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            if (badPrefabs.Count > 0)
                result.AddMessage("The following prefabs' transforms do not fit the requirements", null, badPrefabs.ToArray());
            if (badPrefabsLowOffset.Count > 0)
                result.AddMessage("The following prefabs have unusually low transform values, which might not be accurately displayed " +
                    "in the Inspector window. Please use the 'Debug' Inspector mode to review the Transform component of these prefabs " +
                    "or reset the Transform components using the right-click context menu", null, badPrefabsLowOffset.ToArray());

            return result;
        }

        #endregion

        #region 4_IncludeColliders

        public TestResult _4_IncludeColliders()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var prefabs = GetObjectsFromAssets(FileType.Prefab);
            var badPrefabs = new List<GameObject>();

            foreach (var o in prefabs)
            {
                var p = (GameObject)o;
                var meshes = GetCustomMeshesInObject(p);

                if (!p.isStatic || !meshes.Any())
                    continue;

                var colliders = p.GetComponentsInChildren<Collider>(true);
                if (!colliders.Any())
                    badPrefabs.Add(p);
            }

            if (badPrefabs.Count <= 0)
            {
                result.AddMessage("All found prefabs have colliders!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Warning;
            result.AddMessage("The following prefabs contain meshes, but colliders were not found", null, badPrefabs.ToArray());

            return result;
        }

        #endregion

        #region 5_EmptyPrefab

        public TestResult _5_EmptyPrefab()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var prefabs = GetObjectsFromAssets(FileType.Prefab);
            var badPrefabs = new List<GameObject>();

            foreach (var o in prefabs)
            {
                var p = (GameObject)o;
                if (p.GetComponents<Component>().Length == 1 && p.transform.childCount == 0)
                    badPrefabs.Add(p);
            }

            if (badPrefabs.Count <= 0)
            {
                result.AddMessage("No empty prefabs were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following prefabs are empty", null, badPrefabs.ToArray());

            return result;
        }

        #endregion

        #region 6_IncludeDocumentation

        public TestResult _6_IncludeDocumentation()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var textFiles = GetObjectsFromAssets(FileType.Documentation);
            var documentationFiles = textFiles.Where(x => CouldBeDocumentation(AssetDatabase.GetAssetPath(x))).ToArray();

            if (textFiles.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("No potential documentation files ('.txt', '.pdf', " +
                                  "'.html', '.rtf', '.md') found within the given path.", null, textFiles);
            }
            else if (documentationFiles.Length == 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("The following files have been found to match the documentation file format," +
                    " but may not be documentation in content",
                    null, textFiles);
            }
            else
                result.AddMessage("Found documentation files", null, documentationFiles);

            return result;
        }

        private bool CouldBeDocumentation(string filePath)
        {
            if (filePath.EndsWith(".pdf"))
                return true;

            using (var fs = File.Open(filePath, FileMode.Open))
            using (var bs = new BufferedStream(fs))
            using (var sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    var mentionsDocumentation = line.IndexOf("documentation", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (mentionsDocumentation)
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region 7_FixOrientation

        public TestResult _7_FixOrientation()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var models = GetObjectsFromAssets(FileType.Model);
            var badModels = new List<GameObject>();

            foreach (var o in models)
            {
                var m = (GameObject)o;
                var meshes = GetCustomMeshesInObject(m);
                var assetImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(m));

                if (!(assetImporter is ModelImporter modelImporter))
                    continue;

                var clips = modelImporter.clipAnimations.Length;

                // Only check if the model has meshes and no clips
                if (!meshes.Any() || clips != 0)
                    continue;

                Transform[] transforms = m.GetComponentsInChildren<Transform>(true);

                foreach (var t in transforms)
                {
                    var hasMeshComponent = t.TryGetComponent<MeshFilter>(out _) || t.TryGetComponent<SkinnedMeshRenderer>(out _);

                    if (t.localRotation == Quaternion.identity || !hasMeshComponent)
                        continue;

                    badModels.Add(m);
                    break;
                }
            }

            if (badModels.Count <= 0)
            {
                result.AddMessage("All found models are facing the right way!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Warning;
            result.AddMessage("The following models have incorrect rotation", null, badModels.ToArray());

            return result;
        }

        #endregion

        #region 8_RemoveJPGFiles

        public TestResult _8_RemoveJpgFiles()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var jpgs = GetObjectsFromAssets(FileType.JPG);

            if (jpgs.Length == 0)
            {
                result.AddMessage("No JPG/JPEG textures were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following textures are compressed as JPG/JPEG", null, jpgs);

            return result;
        }

        #endregion

        #region 9_MissingComponentsInAssets

        public TestResult _9_MissingComponentsInAssets()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var assets = GetAllAssetsWithMissingComponents();

            if (assets.Length == 0)
            {
                result.AddMessage("No assets have missing components!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following assets contain missing components", null, assets);

            return result;
        }

        private GameObject[] GetAllAssetsWithMissingComponents()
        {
            var missingReferenceAssets = new List<GameObject>();
            var assetPaths = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith(s_mainFolderPath));

            foreach (var path in assetPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset != null && IsMissingReference(asset))
                    missingReferenceAssets.Add(asset);
            }

            return missingReferenceAssets.ToArray();
        }

        private bool IsMissingReference(GameObject asset)
        {
            var components = asset.GetComponentsInChildren<Component>();

            foreach (var c in components)
            {
                if (!c)
                    return true;
            }

            return false;
        }

        #endregion

        #region 10_MissingComponentTest

        public TestResult _10_MissingComponentTest()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var originalScenePath = SceneManager.GetActiveScene().path;

            var scenes = GetObjectsFromAssets(FileType.Scene).Select(AssetDatabase.GetAssetPath);
            foreach (var scene in scenes)
            {
                var missingComponentGOs = GetMissingComponentGOsInScene(scene);

                if (missingComponentGOs.Count <= 0)
                    continue;

                result.Result = TestResult.ResultStatus.Fail;
                var message = $"GameObjects with missing components or prefab references found in {scene}.\n\nClick this message to open the Scene and see the affected GameObjects:";
                result.AddMessage(message, new MessageActionOpenAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene)), missingComponentGOs.ToArray());
            }

            if (originalScenePath != "")
                EditorSceneManager.OpenScene(originalScenePath);

            if (result.Result == TestResult.ResultStatus.Pass)
                result.AddMessage("No missing components were found!");

            return result;
        }

        private List<GameObject> GetMissingComponentGOsInScene(string path)
        {
            var missingComponentGOs = new List<GameObject>();

            EditorSceneManager.OpenScene(path);
            var scene = SceneManager.GetSceneByPath(path);

            if (!scene.IsValid())
            {
                Debug.LogWarning("Unable to get Scene in " + path);
                return new List<GameObject>();
            }

            var rootObjects = scene.GetRootGameObjects();

            foreach (var obj in rootObjects)
            {
                missingComponentGOs.AddRange(GetMissingComponentGOs(obj));
            }

            return missingComponentGOs;
        }

        private List<GameObject> GetMissingComponentGOs(GameObject root)
        {
            var missingComponentGOs = new List<GameObject>();
            var rootComponents = root.GetComponents<Component>();

            if (PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.MissingAsset || rootComponents.Any(c => !c))
            {
                missingComponentGOs.Add(root);
            }

            foreach (Transform child in root.transform)
                missingComponentGOs.AddRange(GetMissingComponentGOs(child.gameObject));

            return missingComponentGOs;
        }

        #endregion

        #region 11_RemoveJavaScript

        public TestResult _11_RemoveJavaScript()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var javascriptObjects = GetObjectsFromAssets(FileType.JavaScript);

            if (javascriptObjects.Length == 0)
            {
                result.AddMessage("No UnityScript / JS files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following assets are UnityScript / JS files", null, javascriptObjects);

            return result;
        }

        #endregion

        #region 12_RemoveLossyAudioFiles

        public TestResult _12_RemoveLossyAudioFiles()
        {
            string SanitizeForComparison(Object o)
            {
                Regex alphanumericRegex = new Regex("[^a-zA-Z0-9]");
                string path = AssetDatabase.GetAssetPath(o);
                path = path.ToLower();
                
                int extensionIndex = path.LastIndexOf('.');
                string extension = path.Substring(extensionIndex + 1);
                string sanitized = path.Substring(0, extensionIndex);
                
                int separatorIndex = sanitized.LastIndexOf('/');
                sanitized = sanitized.Substring(separatorIndex);
                sanitized = alphanumericRegex.Replace(sanitized, String.Empty);
                sanitized = sanitized.Replace(extension, String.Empty);
                sanitized = sanitized.Trim();

                return sanitized;
            }

            TestResult GetSuccessResult(TestResult res)
            {
                res.AddMessage("No lossy audio files were found!");
                return res;
            }
            
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var lossyAudioObjects = GetObjectsFromAssets(FileType.LossyAudio);
            if (lossyAudioObjects.Length == 0)
                return GetSuccessResult(result);
            
            // Try to find and match variants
            var nonLossyAudioObjects = GetObjectsFromAssets(FileType.NonLossyAudio);
            HashSet<string> nonLossyPathSet = new HashSet<string>();
            foreach(var asset in nonLossyAudioObjects)
            {
                var path = SanitizeForComparison(asset);
                nonLossyPathSet.Add(path);
            }

            List<Object> unmatchedAssets = new List<Object>();
            foreach (var asset in lossyAudioObjects)
            {
                var path = SanitizeForComparison(asset);
                if(!nonLossyPathSet.Contains(path))
                    unmatchedAssets.Add(asset);
            }

            if (unmatchedAssets.Count == 0)
                return GetSuccessResult(result);

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following lossy audio files were found without identically named non-lossy variants:", null, unmatchedAssets.ToArray()); 
            return result;
        }

        #endregion

        #region 13_RemoveVideoFiles

        public TestResult _13_RemoveVideoFiles()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var videos = GetObjectsFromAssets(FileType.Video);

            if (videos.Length == 0)
            {
                result.AddMessage("No video files were found, looking good!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following video files were found", null, videos);

            return result;
        }

        #endregion

        #region 14_RemoveExecutableFiles

        public TestResult _14_RemoveExecutableFiles()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var executables = GetObjectsFromAssets(FileType.Executable);

            if (executables.Length == 0)
            {
                result.AddMessage("No executable files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following executable files were found", null, executables);

            return result;
        }

        #endregion

        #region 15_RemoveMixamoFiles

        public TestResult _15_RemoveMixamoFiles()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var mixamoFiles = GetObjectsFromAssets(FileType.Mixamo);

            if (mixamoFiles.Length == 0)
            {
                result.AddMessage("No Mixamo files were found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following Mixamo files were found", null, mixamoFiles);

            return result;
        }

        #endregion

        #region 16_RemoveSpeedTreeFiles

        public TestResult _16_RemoveSpeedTreeFiles()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var speedtreeObjects = GetObjectsFromAssets(FileType.SpeedTree);

            if (speedtreeObjects.Length == 0)
            {
                result.AddMessage("No SpeedTree assets have been found!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Fail;
            result.AddMessage("The following SpeedTree assets have been found", null, speedtreeObjects);

            return result;
        }

        #endregion

        #region 17_CheckLODsOnYourPrefabs

        public TestResult _17_CheckLODsonyourPrefabs()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var prefabs = GetObjectsFromAssets(FileType.Prefab);
            var badPrefabs = new List<GameObject>();

            foreach (var o in prefabs)
            {
                var p = (GameObject)o;
                var meshFilters = p.GetComponentsInChildren<MeshFilter>();
                var hasLODGroup = p.TryGetComponent<LODGroup>(out _);

                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf.name.Contains("LOD") && !hasLODGroup)
                        badPrefabs.Add(p);
                }
            }

            if (badPrefabs.Count <= 0)
            {
                result.AddMessage("All found prefabs are meeting the LOD requirements!");
                return result;
            }

            result.Result = TestResult.ResultStatus.Warning;
            result.AddMessage("The following prefabs do not meet the LOD requirements", null, badPrefabs.ToArray());

            return result;
        }

        #endregion

        #region 18_ShaderCompilerErrors

        public TestResult _18_ShaderCompilerErrors()
        {
            TestResult result = new TestResult
            {
                Result = TestResult.ResultStatus.Pass
            };

            var shaders = GetObjectsFromAssets(FileType.Shader);
            var badShaders = shaders.Where(ShaderHasError).ToArray();

            if (badShaders.Length > 0)
            {
                result.Result = TestResult.ResultStatus.Fail;
                result.AddMessage("The following shader files have errors", null, badShaders);
            }
            else
            {
                result.AddMessage("All found Shaders have no compilation errors!");
            }

            return result;
        }

        private bool ShaderHasError(UnityEngine.Object obj)
        {
            switch (obj)
            {
                case Shader shader:
                    return ShaderUtil.ShaderHasError(shader);
                case ComputeShader shader:
                    return ShaderUtil.GetComputeShaderMessageCount(shader) > 0;
                case RayTracingShader shader:
                    return ShaderUtil.GetRayTracingShaderMessageCount(shader) > 0;
                default:
                    return false;
            }
        }

        #endregion

        #region 19_TypesHaveNamespaces

        public TestResult _19_TypesHaveNamespaces()
        {
            TestResult result = new TestResult();

            var scripts = GetObjectsFromAssets(FileType.MonoScript).Select(x => x as MonoScript).ToList();
            var affectedScripts = NamespaceUtility.GetTypesWithoutNamespacesFromScripts(scripts);

            var dlls = GetObjectsFromAssets(FileType.PrecompiledAssembly).ToList();
            var affectedDlls = NamespaceUtility.GetTypesWithoutNamespacesFromAssemblies(dlls);

            if (affectedScripts.Count > 0 || affectedDlls.Count > 0)
            {
                if (affectedScripts.Count > 0)
                {
                    result.Result = TestResult.ResultStatus.Warning;
                    var objects = affectedScripts.Keys.ToArray();
                    result.AddMessage("The following scripts contain types (classes, interfaces, structs or enums) not nested in a namespace:");
                    foreach (var kvp in affectedScripts)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }

                if (affectedDlls.Count > 0)
                {
                    result.Result = TestResult.ResultStatus.Warning;
                    result.AddMessage("The following precompiled assemblies contain types not nested in a namespace:");
                    foreach (var kvp in affectedDlls)
                    {
                        var message = string.Empty;
                        foreach (var type in kvp.Value)
                            message += type + "\n";

                        message = message.Remove(message.Length - "\n".Length);
                        result.AddMessage(message, null, kvp.Key);
                    }
                }
            }
            else
                result.Result = TestResult.ResultStatus.Pass;

            return result;
        }

        #endregion

        #region 20_ConsistentLineEndings

        public TestResult _20_ConsistentLineEndings()
        {
            TestResult result = new TestResult();

            var scripts = GetObjectsFromAssets(FileType.MonoScript).Select(x => x as MonoScript).ToArray();

            var affectedScripts = new ConcurrentBag<Object>();
            var scriptContents = new ConcurrentDictionary<MonoScript, string>();

            // A separate dictionary is needed because MonoScript contents cannot be accessed outside of the main thread
            foreach (var s in scripts)
                if(s != null)
                    scriptContents.TryAdd(s, s.text);

            Parallel.ForEach(scriptContents, (s) =>
            {
               if(HasInconsistentLineEndings(s.Value))
                    affectedScripts.Add(s.Key);
            });

            if (affectedScripts.Count > 0)
            {
                result.Result = TestResult.ResultStatus.Warning;
                result.AddMessage("The following scripts have inconsistent line endings:", null, affectedScripts.ToArray());
            }
            else
                result.Result = TestResult.ResultStatus.Pass;

            return result;
        }

        private bool HasInconsistentLineEndings(string text)
        {
            int crlfEndings = 0;
            int lfEndings = 0;

            var split = text.Split(new[] { "\n" }, StringSplitOptions.None);
            for (int i = 0; i < split.Length; i++)
            {
                var line = split[i];
                if (line.EndsWith("\r"))
                    crlfEndings++;
                else if (i != split.Length - 1)
                    lfEndings++;
            }

            if (crlfEndings > 0 && lfEndings > 0)
                return true;
            return false;
        }

        #endregion
    }
}
