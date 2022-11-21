using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AssetStoreTools.Validator;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader
{
    internal class ValidationElement : VisualElement
    {
        private Button _validateButton;
        private Button _viewReportButton;
        
        private VisualElement _infoBox;
        private Label _infoBoxLabel;
        private Image _infoBoxImage;

        private string _localPath;
        private string _category;

        private CategoryEvaluator _categoryEvaluator;
        private readonly Dictionary<int, AutomatedTestElement> _testElements = new Dictionary<int, AutomatedTestElement>();

        public ValidationElement()
        {
            ConstructValidationElement();
            EnableValidation(false);
        }

        public void SetLocalPath(string path)
        {
            _localPath = path;
            
            EnableValidation(true);
        }
        
        public void SetCategory(string category)
        {
            _category = category;
        }

        private void ConstructValidationElement()
        {
            VisualElement validatorButtonRow = new VisualElement();
            validatorButtonRow.AddToClassList("selection-box-row");

            VisualElement validatorLabelHelpRow = new VisualElement();
            validatorLabelHelpRow.AddToClassList("label-help-row");

            Label validatorLabel = new Label { text = "Validation" };
            Image validatorLabelTooltip = new Image
            {
                tooltip = "You can use the Asset Store Validator to check your package for common publishing issues"
            };
            
            _validateButton = new Button(GetOutcomeResults) { name = "ValidateButton", text = "Validate" };
            _validateButton.AddToClassList("validation-button");
            
            validatorLabelHelpRow.Add(validatorLabel);
            validatorLabelHelpRow.Add(validatorLabelTooltip);

            validatorButtonRow.Add(validatorLabelHelpRow);
            validatorButtonRow.Add(_validateButton);

            Add(validatorButtonRow);

            SetupInfoBox("");
        }

        private async void GetOutcomeResults()
        {
            TestActions testActions = TestActions.Instance;
            testActions.SetMainPath(_localPath);

            ValidationState.Instance.SetMainPath(_localPath);
            ValidationState.Instance.SetCategory(_category);
            _validateButton.SetEnabled(false);

            _categoryEvaluator = new CategoryEvaluator(_category);

            var testsPath = "Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/Tests";
            var testObjects = ValidatorUtility.GetAutomatedTestCases(testsPath, true);
            var automatedTests = testObjects.Select(t => new AutomatedTest(t)).ToList();

            // Make sure everything is collected and validation button is disabled
            await Task.Delay(100);

            var outcomeList = new List<TestResult>();
            _testElements.Clear();

            for (int i = 0; i < automatedTests.Count; i++)
            {
                var test = automatedTests[i];
                try
                {
                    var testElement = new AutomatedTestElement(test);
                    _testElements.Add(test.Id, testElement);

                    test.OnTestComplete += OnTestComplete;
                    EditorUtility.DisplayProgressBar("Validating", $"Running validation: {i + 1} - {test.Title}", (float)i / automatedTests.Count);
                    test.Run();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    return;
                }

                outcomeList.Add(test.Result);
            }

            EditorUtility.ClearProgressBar();

            EnableInfoBox(true, outcomeList);
            _validateButton.SetEnabled(true);

            ValidationState.Instance.SaveJson();
        }
        
        private void EnableValidation(bool enable)
        {
            style.display = enable ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void EnableInfoBox(bool enable, List<TestResult> outcomeList)
        {
            if (!enable)
            {
                _infoBox.style.display = DisplayStyle.None;
                return;
            }
            
            var errorCount = outcomeList.Count(x => x.Result == TestResult.ResultStatus.Fail);
            var warningCount = outcomeList.Count(x => x.Result == TestResult.ResultStatus.Warning);
            var passCount = outcomeList.Count(x => x.Result == TestResult.ResultStatus.Pass);
            
            _infoBox.Q<Label>().text = $"{errorCount} errors, {warningCount} warnings, {passCount} passed";

            if (errorCount > 0)
                _infoBoxImage.image = EditorGUIUtility.IconContent("console.erroricon@2x").image;
            else if (warningCount > 0)
                _infoBoxImage.image = EditorGUIUtility.IconContent("console.warnicon@2x").image;

            _validateButton.text = "Re-validate";
            _infoBox.style.display = DisplayStyle.Flex;
        }
        
        private void SetupInfoBox(string infoText)
        {
            _infoBox = new Box { name = "InfoBox" };
            _infoBox.style.display = DisplayStyle.None;
            _infoBox.AddToClassList("info-box");

            _infoBoxImage = new Image();
            _infoBoxLabel = new Label { name = "ValidationLabel", text = infoText };
            _viewReportButton = new Button (ViewReport) {text = "View report"};
            _viewReportButton.AddToClassList("hyperlink-button");
            
            _infoBox.Add(_infoBoxImage);
            _infoBox.Add(_infoBoxLabel);
            _infoBox.Add(_viewReportButton);

            Add(_infoBox);
        }

        private void ViewReport()
        {
            // Re-run validation if path is out of sync
            if (ValidationState.Instance.ValidationStateData.SerializedMainPath != _localPath)
                GetOutcomeResults();
            
            // Re-run validation if category is out of sync
            if (ValidationState.Instance.ValidationStateData.SerializedCategory != _category)
                GetOutcomeResults();
            
            // Show the Validator
            AssetStoreTools.ShowAssetStoreToolsValidator();
        }

        private void OnTestComplete(int id, TestResult result)
        {
            var testElement = _testElements[id];
            var test = testElement.GetAutomatedTest();
            
            result.Result = _categoryEvaluator.Evaluate(test);
            test.Result = result;

            ValidationState.Instance.ChangeResult(id, result);
        }
    }
}