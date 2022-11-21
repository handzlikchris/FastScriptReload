using AssetStoreTools.Utility.Json;
using AssetStoreTools.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader
{
    internal class HybridPackageUploadWorkflowView : UploadWorkflowView
    {
        public const string WorkflowName = "HybridPackageWorkflow";
        public const string WorkflowDisplayName = "Local UPM Package";

        public override string Name => WorkflowName;
        public override string DisplayName => WorkflowDisplayName;

        private string _category;

        private ValidationElement _validationElement;
        private VisualElement _extraPackagesElement;

        private HybridPackageUploadWorkflowView(string category, Action serializeSelection) : base(serializeSelection)
        {
            _category = category;
            
            SetupWorkflow();
        }

        public static HybridPackageUploadWorkflowView Create(string category, Action serializeAction)
        {
            return new HybridPackageUploadWorkflowView(category, serializeAction);
        }

        protected sealed override void SetupWorkflow()
        {
            // Path selection
            VisualElement folderPathSelectionRow = new VisualElement();
            folderPathSelectionRow.AddToClassList("selection-box-row");

            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");

            Label folderPathLabel = new Label { text = "Package path" };
            Image folderPathLabelTooltip = new Image
            {
                tooltip = "Select a local Package you would like to export and upload to the Store."
            };

            labelHelpRow.Add(folderPathLabel);
            labelHelpRow.Add(folderPathLabelTooltip);

            PathSelectionField = new TextField();
            PathSelectionField.AddToClassList("path-selection-field");
            PathSelectionField.isReadOnly = true;

            Button browsePathButton = new Button(BrowsePath) { name = "BrowsePathButton", text = "Browse" };
            browsePathButton.AddToClassList("browse-button");

            folderPathSelectionRow.Add(labelHelpRow);
            folderPathSelectionRow.Add(PathSelectionField);
            folderPathSelectionRow.Add(browsePathButton);

            Add(folderPathSelectionRow);

            _validationElement = new ValidationElement();
            Add(_validationElement);
            
            _validationElement.SetCategory(_category);
        }

        public override void LoadSerializedWorkflow(JsonValue json, string lastUploadedPath, string lastUploadedGuid)
        {
            if(!DeserializeMainExportPath(json, out string mainExportPath) || !Directory.Exists(mainExportPath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow paths from local cache");
                LoadSerializedWorkflowFallback(lastUploadedGuid, lastUploadedGuid);
                return;
            }

            DeserializeExtraExportPaths(json, out List<string> extraExportPaths);

            ASDebug.Log($"Restoring serialized Hybrid Package workflow values from local cache");
            LoadSerializedWorkflow(mainExportPath, extraExportPaths);
        }

        public override void LoadSerializedWorkflowFallback(string lastUploadedPath, string lastUploadedGuid)
        {
            var mainExportPath = AssetDatabase.GUIDToAssetPath(lastUploadedGuid);
            if (string.IsNullOrEmpty(mainExportPath))
                mainExportPath = lastUploadedPath;
            
            if (!mainExportPath.StartsWith("Packages/") || !Directory.Exists(mainExportPath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow paths from previous upload values");
                return;
            }

            ASDebug.Log($"Restoring serialized Hybrid Package workflow values from previous upload values");
            LoadSerializedWorkflow(mainExportPath, null);
        }

        private void LoadSerializedWorkflow(string relativeAssetDatabasePath, List<string> extraExportPaths)
        {
            // Expected path is in ADB form, so we need to reconstruct it first
            var rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);
            var realPath = Path.GetFullPath(relativeAssetDatabasePath).Replace('\\', '/');
            if (realPath.StartsWith(rootProjectPath))
                realPath = realPath.Substring(rootProjectPath.Length);

            if (!IsValidLocalPackage(realPath, out relativeAssetDatabasePath))
            {
                ASDebug.Log("Unable to restore Hybrid Package workflow path - package is not a valid UPM package");
                return;
            }

            // Treat this as a manual selection
            HandleHybridUploadPathSelection(realPath, relativeAssetDatabasePath, extraExportPaths, false);
        }

        protected override void BrowsePath()
        {
            // Path retrieval
            string relativeExportPath = string.Empty;
            string rootProjectPath = Application.dataPath.Substring(0, Application.dataPath.Length - "Assets".Length);

            var absoluteExportPath = EditorUtility.OpenFolderPanel("Select the Package", "Packages/", "");

            if (string.IsNullOrEmpty(absoluteExportPath))
                return;

            if (absoluteExportPath.StartsWith(rootProjectPath))
                relativeExportPath = absoluteExportPath.Substring(rootProjectPath.Length);

            var workingPath = !string.IsNullOrEmpty(relativeExportPath) ? relativeExportPath : absoluteExportPath;
            if (!IsValidLocalPackage(workingPath, out string relativeAssetDatabasePath))
            {
                EditorUtility.DisplayDialog("Invalid selection", "Selected export path must be a valid local package", "OK");
                return;
            }

            HandleHybridUploadPathSelection(workingPath, relativeAssetDatabasePath, null, true);
        }

        private void HandleHybridUploadPathSelection(string relativeExportPath, string relativeAssetDatabasePath, List<string> serializedToggles, bool serializeValues)
        {
            PathSelectionField.value = relativeExportPath + "/";

            // Reset and reinitialize the selected export path(s) array
            MainExportPath = relativeAssetDatabasePath;
            ExtraExportPaths = new List<string>();

            // Set additional upload data for the Publisher Portal backend (GUID and Package Path).
            // The backend workflow currently accepts only 1 package guid and path, so we'll use the main folder data
            LocalPackageGuid = AssetDatabase.AssetPathToGUID(relativeAssetDatabasePath);
            LocalPackagePath = relativeAssetDatabasePath;
            LocalProjectPath = relativeAssetDatabasePath;

            _validationElement.SetLocalPath(relativeAssetDatabasePath);

            if (_extraPackagesElement != null)
            {
                _extraPackagesElement.Clear();
                Remove(_extraPackagesElement);

                _extraPackagesElement = null;
            }

            List<string> pathsToAdd = new List<string>();
            foreach (var package in PackageUtility.GetAllLocalPackages())
            {
                // Exclude the Asset Store Tools themselves
                if (package.name == "com.unity.asset-store-tools")
                    continue;

                var localPackagePath = package.GetConvenientPath();

                if (localPackagePath == relativeExportPath)
                    continue;

                pathsToAdd.Add(package.assetPath);
            }

            if (pathsToAdd.Count != 0)
                PopulateExtraPathsBox(pathsToAdd, serializedToggles);

            if (serializeValues)
                SerializeSelection?.Invoke();
        }

        private void PopulateExtraPathsBox(List<string> otherPackagesFound, List<string> checkedToggles)
        {
            // Dependencies selection
            _extraPackagesElement = new VisualElement();
            _extraPackagesElement.AddToClassList("selection-box-row");

            VisualElement extraPackagesHelpRow = new VisualElement();
            extraPackagesHelpRow.AddToClassList("label-help-row");

            Label extraPackagesLabel = new Label { text = "Extra Packages" };
            Image extraPackagesLabelTooltip = new Image
            {
                tooltip = "If your package has dependencies on other local packages, please select which of these packages should also be included in the resulting package"
            };

            VisualElement extraPackagesTogglesBox = new ScrollView { name = "ExtraPackagesTogglesToggles" };
            extraPackagesTogglesBox.AddToClassList("extra-packages-scroll-view");

            extraPackagesHelpRow.Add(extraPackagesLabel);
            extraPackagesHelpRow.Add(extraPackagesLabelTooltip);

            _extraPackagesElement.Add(extraPackagesHelpRow);
            _extraPackagesElement.Add(extraPackagesTogglesBox);

            EventCallback<ChangeEvent<bool>, string> toggleChangeCallback = OnToggledPackage;

            foreach (var path in otherPackagesFound)
            {
                var toggle = new Toggle { value = false, text = path };
                toggle.AddToClassList("extra-packages-toggle");
                toggle.tooltip = path;
                if (checkedToggles != null && checkedToggles.Contains(toggle.text))
                {
                    toggle.SetValueWithoutNotify(true);
                    ExtraExportPaths.Add(toggle.text);
                }

                toggle.RegisterCallback(toggleChangeCallback, toggle.text);
                extraPackagesTogglesBox.Add(toggle);
            }

            Add(_extraPackagesElement);
        }

        private void OnToggledPackage(ChangeEvent<bool> evt, string folderPath)
        {
            switch (evt.newValue)
            {
                case true when !ExtraExportPaths.Contains(folderPath):
                    ExtraExportPaths.Add(folderPath);
                    break;
                case false when ExtraExportPaths.Contains(folderPath):
                    ExtraExportPaths.Remove(folderPath);
                    break;
            }

            SerializeSelection?.Invoke();
        }

        private bool IsValidLocalPackage(string packageFolderPath, out string assetDatabasePackagePath)
        {
            assetDatabasePackagePath = string.Empty;

            string packageManifestPath = $"{packageFolderPath}/package.json";

            if (!File.Exists(packageManifestPath))
                return false;
            try
            {
                var localPackages = PackageUtility.GetAllLocalPackages();

                if (localPackages == null || localPackages.Length == 0)
                    return false;

                foreach (var package in localPackages)
                {
                    var localPackagePath = package.GetConvenientPath();

                    if (localPackagePath != packageFolderPath)
                        continue;

                    assetDatabasePackagePath = package.assetPath;
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public override async Task<PackageExporter.ExportResult> ExportPackage(string packageName, bool _)
        {
            var paths = GetAllExportPaths();
            var outputPath = $"Temp/{packageName}-{DateTime.Now:yyyy-dd-M--HH-mm-ss}.unitypackage";
            return await PackageExporter.ExportPackage(paths, outputPath, false, false, false);
        }
    }
}