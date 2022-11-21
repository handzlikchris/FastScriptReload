using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using AssetStoreTools.Utility;
using AssetStoreTools.Utility.Json;

namespace AssetStoreTools.Uploader
{
    internal static class PackageExporter
    {
        private const string ExportMethodWithoutDependencies = "UnityEditor.PackageUtility.ExportPackage";
        private const string ExportMethodWithDependencies = "UnityEditor.PackageUtility.ExportPackageAndPackageManagerManifest";

        private const string ProgressBarTitle = "Exporting Package";
        private const string ProgressBarStep1 = "Saving Assets...";
        private const string ProgressBarStep2 = "Gathering files...";
        private const string ProgressBarStep3 = "Compressing package...";

        private const string TemporaryExportPathName = "CustomExport";
        private const string PackagesLockPath = "Packages/packages-lock.json";
        private const string ManifestJsonPath = "Packages/manifest.json";
        
        internal class ExportResult
        {
            public bool Success;
            public string ExportedPath;
            public ASError Error;

            public static implicit operator bool(ExportResult value)
            {
                return value != null && value.Success;
            }
        }

        public static async Task<ExportResult> ExportPackage(string[] exportPaths, string outputFilename,
            bool includeDependencies, bool isCompleteProject, bool useLegacyExporter = false, string[] dependencies = null)
        {
            if (exportPaths == null || exportPaths.Length == 0)
                return new ExportResult() { Success = false, Error = ASError.GetGenericError(new ArgumentException("Package Exporting failed: received an invalid export paths array")) };

            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStep1, 0.1f);
            AssetDatabase.SaveAssets();

            if (isCompleteProject)
                exportPaths = IncludeProjectSettings(exportPaths);

            try
            {
                if (useLegacyExporter)
                    await ExportPackageNative(exportPaths, outputFilename, includeDependencies);
                else
                    ExportPackageCustom(exportPaths, outputFilename, includeDependencies, dependencies);

                ASDebug.Log($"Package file has been created at {outputFilename}");
                return new ExportResult() { Success = true, ExportedPath = outputFilename };
            }
            catch (Exception e)
            {
                return new ExportResult() { Success = false, Error = ASError.GetGenericError(e) };
            }
            finally
            {
                PostExportCleanup();
            }
        }

        private static string[] IncludeProjectSettings(string[] exportPaths)
        {
            var updatedExportPaths = new string[exportPaths.Length + 1];
            exportPaths.CopyTo(updatedExportPaths, 0);
            updatedExportPaths[updatedExportPaths.Length - 1] = "ProjectSettings";
            return updatedExportPaths;
        }

        private static async Task ExportPackageNative(string[] exportPaths, string outputFilename, bool includeDependencies)
        {
            ASDebug.Log("Using native package exporter");
            var guids = GetGuids(exportPaths, out bool onlyFolders);

            if (guids.Length == 0 || onlyFolders)
                throw new ArgumentException("Package Exporting failed: provided export paths are empty or only contain empty folders");

            string exportMethod = ExportMethodWithoutDependencies;
            if (includeDependencies)
                exportMethod = ExportMethodWithDependencies;

            var split = exportMethod.Split('.');
            var assembly = Assembly.Load(split[0]); // UnityEditor
            var typeName = $"{split[0]}.{split[1]}"; // UnityEditor.PackageUtility
            var methodName = split[2]; // ExportPackage or ExportPackageAndPackageManagerManifest

            var type = assembly.GetType(typeName);
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, new Type[] { typeof(string[]), typeof(string) }, null);

            ASDebug.Log("Invoking native export method");

            method?.Invoke(null, new object[] { guids, outputFilename });

            // The internal exporter methods are asynchronous, therefore
            // we need to wait for exporting to finish before returning
            await Task.Run(() =>
            {
                while (!File.Exists(outputFilename))
                    Thread.Sleep(100);
            });
        }

        private static string[] GetGuids(string[] exportPaths, out bool onlyFolders)
        {
            var guids = new List<string>();
            onlyFolders = true;

            foreach (var exportPath in exportPaths)
            {
                var assetPaths = GetAssetPaths(exportPath);

                foreach (var assetPath in assetPaths)
                {
                    var guid = GetAssetGuid(assetPath, false);
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    guids.Add(guid);
                    if (onlyFolders == true && (File.Exists(assetPath)))
                        onlyFolders = false;
                }
            }

            return guids.ToArray();
        }

        private static string[] GetAssetPaths(string rootPath)
        {
            // To-do: slight optimization is possible in the future by having a list of excluded folders/file extensions
            List<string> paths = new List<string>();

            // Add files within given directory
            var filePaths = Directory.GetFiles(rootPath).Select(p => p.Replace('\\', '/')).ToArray();
            paths.AddRange(filePaths);

            // Add directories within given directory
            var directoryPaths = Directory.GetDirectories(rootPath).Select(p => p.Replace('\\', '/')).ToArray();
            foreach (var nestedDirectory in directoryPaths)
                paths.AddRange(GetAssetPaths(nestedDirectory));

            // Add the given directory itself if it is not empty
            if (filePaths.Length > 0 || directoryPaths.Length > 0)
                paths.Add(rootPath);

            return paths.ToArray();
        }

        private static string GetAssetGuid(string assetPath, bool hiddenSearch)
        {
            // Skip meta files as they do not have guids
            if (assetPath.EndsWith(".meta"))
                return string.Empty;

            // Attempt retrieving guid from the Asset Database first
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (guid != string.Empty)
                return guid;

            // Files in hidden folders (e.g. Samples~) are not part of the Asset Database,
            // therefore GUIDs need to be scraped from the .meta file.
            // Note: only do this for custom exporter since the native exporter
            // will not be able to retrieve the asset path from a hidden folder
            if (hiddenSearch)
            {
                // To-do: handle hidden folders without meta files
                var metaPath = $"{assetPath}.meta";

                if (!File.Exists(metaPath))
                    return string.Empty;

                using (StreamReader reader = new StreamReader(metaPath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != string.Empty)
                    {
                        if (!line.StartsWith("guid:"))
                            continue;
                        var metaGuid = line.Substring("guid:".Length).Trim();
                        return metaGuid;
                    }
                }
            }

            return string.Empty;
        }

        private static void PostExportCleanup()
        {
            EditorUtility.ClearProgressBar();
            var tempExportPath = GetTemporaryExportPath();
            if (Directory.Exists(tempExportPath))
                Directory.Delete(tempExportPath, true);
        }

        #region Experimental

        private static void ExportPackageCustom(string[] exportPaths, string outputFilename, bool includeDependencies, string[] dependencies)
        {
            ASDebug.Log("Using custom package exporter");
            // Create a temporary export path
            var temporaryExportPath = GetTemporaryExportPath();
            if (!Directory.Exists(temporaryExportPath))
                Directory.CreateDirectory(temporaryExportPath);

            // Construct an unzipped package structure
            CreateTempPackageStructure(exportPaths, temporaryExportPath, includeDependencies, dependencies);

            // Build a .unitypackage file from the temporary folder
            CreateUnityPackage(temporaryExportPath, outputFilename);

            EditorUtility.RevealInFinder(outputFilename);
        }

        private static string GetTemporaryExportPath()
        {
            return $"{AssetStoreCache.TempCachePath}/{TemporaryExportPathName}";
        }

        private static void CreateTempPackageStructure(string[] exportPaths, string tempOutputPath, bool includeDependencies, string[] dependencies)
        {
            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStep2, 0.4f);
            var pathGuidPairs = GetPathGuidPairs(exportPaths);

            // Caching asset previews takes time, so we'll start doing it as we
            // iterate through assets and only retrieve them after generating the rest
            // of the package structure
            AssetPreview.SetPreviewTextureCacheSize(pathGuidPairs.Count + 100);
            var pathObjectPairs = new Dictionary<string, UnityEngine.Object>();

            foreach (var pair in pathGuidPairs)
            {
                var originalAssetPath = pair.Key;
                var outputAssetPath = $"{tempOutputPath}/{pair.Value}";
                Directory.CreateDirectory(outputAssetPath);

                // Every exported asset has a pathname file
                using (StreamWriter writer = new StreamWriter($"{outputAssetPath}/pathname"))
                    writer.Write(originalAssetPath);

                // Only files (not folders) have an asset file
                if (File.Exists(originalAssetPath))
                    File.Copy(originalAssetPath, $"{outputAssetPath}/asset");

                // Most files and folders have an asset.meta file (but ProjectSettings folder assets do not)
                if (File.Exists($"{originalAssetPath}.meta"))
                    File.Copy($"{originalAssetPath}.meta", $"{outputAssetPath}/asset.meta");

                // To-do: handle previews in hidden folders as they are not part of the AssetDatabase
                var previewObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(originalAssetPath);
                if (previewObject == null)
                    continue;
                // Start caching the asset preview
                AssetPreview.GetAssetPreview(previewObject);
                pathObjectPairs.Add(outputAssetPath, previewObject);
            }

            WritePreviewTextures(pathObjectPairs);

            if (!includeDependencies || dependencies == null || dependencies.Length == 0)
                return;

            var manifestJson = GetPackageManifestJson();
            var allDependenciesDict = manifestJson["dependencies"].AsDict();

            var allLocalPackages = PackageUtility.GetAllLocalPackages();
            List<string> allPackagesList = new List<string>(allDependenciesDict.Keys);
            
            foreach (var package in allPackagesList)
            {
                if (!dependencies.Contains(package))
                {
                    allDependenciesDict.Remove(package);
                    continue;
                }

                if (!allLocalPackages.Select(x => x.name).Contains(package))
                    continue;
                
                allDependenciesDict.Remove(package);
                UnityEngine.Debug.LogWarning($"Found an unsupported Package Manager dependency: {package}.\n" +
                                             "This dependency is not supported in the project's manifest.json and will be skipped.");
            }

            if (allDependenciesDict.Count == 0)
                return;

            var tempManifestDirectoryPath = $"{tempOutputPath}/packagemanagermanifest";
            Directory.CreateDirectory(tempManifestDirectoryPath);
            var tempManifestFilePath = $"{tempManifestDirectoryPath}/asset";

            File.WriteAllText(tempManifestFilePath, manifestJson.ToString());
        }

        private static Dictionary<string, string> GetPathGuidPairs(string[] exportPaths)
        {
            var pathGuidPairs = new Dictionary<string, string>();

            foreach (var exportPath in exportPaths)
            {
                var assetPaths = GetAssetPaths(exportPath);

                foreach (var assetPath in assetPaths)
                {
                    var guid = GetAssetGuid(assetPath, true);
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    pathGuidPairs.Add(assetPath, guid);
                }
            }

            return pathGuidPairs;
        }

        private static void WritePreviewTextures(Dictionary<string, UnityEngine.Object> pathObjectPairs)
        {
            foreach (var kvp in pathObjectPairs)
            {
                var obj = kvp.Value;
                var queuePreview = false;

                switch (obj)
                {
                    case Material _:
                    case TerrainLayer _:
                    case AudioClip _:
                    case Mesh _:
                    case Texture _:
                    case UnityEngine.Tilemaps.Tile _:
                    case GameObject _:
                        queuePreview = true;
                        break;
                }

                if (!queuePreview)
                    continue;

                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _);
                var preview = GetAssetPreviewFromGuid(guid);

                if (!preview)
                    continue;
                
                var thumbnailWidth = Mathf.Min(preview.width, 128);
                var thumbnailHeight = Mathf.Min(preview.height, 128);
                var rt = RenderTexture.GetTemporary(thumbnailWidth, thumbnailHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
                
                var copy = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
                
                RenderTexture.active = rt;
                GL.Clear(true, true, new Color(0, 0, 0, 0));
                Graphics.Blit(preview, rt);
                copy.ReadPixels(new Rect(0, 0, copy.width, copy.height), 0, 0, false);
                copy.Apply();
                RenderTexture.active = null;
                    
                var bytes = copy.EncodeToPNG();
                if (bytes != null && bytes.Length > 0)
                {
                    File.WriteAllBytes(kvp.Key + "/preview.png", bytes);
                }
                    
                RenderTexture.ReleaseTemporary(rt);
            }
        }
        
        private static Texture2D GetAssetPreviewFromGuid(string guid)
        {
            var method = typeof(AssetPreview).GetMethod("GetAssetPreviewFromGUID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string) }, null);
            var args = new object[] { guid };
 
            return method?.Invoke(null, args) as Texture2D;
        }
        
        private static void CreateUnityPackage(string pathToArchive, string outputPath)
        {
            if (Directory.GetDirectories(pathToArchive).Length == 0)
                throw new InvalidOperationException("Unable to export package. The specified path is empty");

            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarStep3, 0.5f);

            // Archiving process working path will be set to the
            // temporary package path so adjust the output path accordingly
            if (!Path.IsPathRooted(outputPath))
                outputPath = $"{Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length)}/{outputPath}";

#if UNITY_EDITOR_WIN
            CreateUnityPackageUniversal(pathToArchive, outputPath);
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            CreateUnityPackageOsxLinux(pathToArchive, outputPath);
#endif
        }

        private static void CreateUnityPackageUniversal(string pathToArchive, string outputPath)
        {
            var _7zPath = EditorApplication.applicationContentsPath;
#if UNITY_EDITOR_WIN
            _7zPath = Path.Combine(_7zPath, "Tools", "7z.exe");
#elif UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            _7zPath = Path.Combine(_7zPath, "Tools", "7za");
#endif
            if (!File.Exists(_7zPath))
                throw new FileNotFoundException("Archiving utility was not found in your Unity installation directory");

            var argumentsTar = $"a -r -ttar -y -bd archtemp.tar .";
            var result = StartProcess(_7zPath, argumentsTar, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");

            // Create a GZIP archive
            var argumentsGzip = $"a -tgzip -bd -y \"{outputPath}\" archtemp.tar";
            result = StartProcess(_7zPath, argumentsGzip, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");
        }

        private static void CreateUnityPackageOsxLinux(string pathToArchive, string outputPath)
        {
            var tarPath = "/usr/bin/tar";

            if (!File.Exists(tarPath))
            {
                // Fallback to the universal export method
                ASDebug.LogWarning("'/usr/bin/tar' executable not found. Falling back to 7za");
                CreateUnityPackageUniversal(pathToArchive, outputPath);
                return;
            }

            // Create a TAR archive
            var arguments = $"-czpf \"{outputPath}\" .";
            var result = StartProcess(tarPath, arguments, pathToArchive);
            if (result != 0)
                throw new Exception("Failed to compress the package");
        }

        private static int StartProcess(string processPath, string arguments, string workingDirectory)
        {
            var info = new ProcessStartInfo()
            {
                FileName = processPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using (Process process = Process.Start(info))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        #endregion

        #region Utility
        
        private static JsonValue GetPackageManifestJson()
        {
            string manifestJsonString = File.ReadAllText(ManifestJsonPath);
            JSONParser parser = new JSONParser(manifestJsonString);
            var manifestJson = parser.Parse();

            return manifestJson;
        }

        #endregion
    }
}