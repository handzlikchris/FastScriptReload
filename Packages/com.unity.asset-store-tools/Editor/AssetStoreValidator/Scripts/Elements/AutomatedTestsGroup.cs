using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator
{
    internal class AutomatedTestsGroup : VisualElement
    {
        private const string TestsPath = "Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/Tests";
        
        private readonly Dictionary<int, AutomatedTestElement> _testElements = new Dictionary<int, AutomatedTestElement>();
        private readonly Dictionary<TestResult.ResultStatus, AutomatedTestsGroupElement> _testGroupElements = 
            new Dictionary<TestResult.ResultStatus, AutomatedTestsGroupElement>();

        private List<AutomatedTest> _automatedTests = new List<AutomatedTest>();
        private CategoryEvaluator _categoryEvaluator;

        private ScrollView _allTestsScrollView;
        private ValidationInfoElement _validationInfoBox;
        private PathBoxElement _pathBox;
        private Button _validateButton;
        private ToolbarMenu _categoryMenu;

        private static readonly TestResult.ResultStatus[] StatusOrder = {TestResult.ResultStatus.Undefined, 
            TestResult.ResultStatus.Fail, TestResult.ResultStatus.Warning, TestResult.ResultStatus.Pass};
        
        public AutomatedTestsGroup()
        {
            ConstructInfoPart();
            ConstructAutomatedTests();

            ValidationState.Instance.OnJsonSave -= Reinitialize;
            ValidationState.Instance.OnJsonSave += Reinitialize;
        }

        private void Reinitialize()
        {
            this.Clear();
            
            _testElements.Clear();
            _testGroupElements.Clear();
            _automatedTests.Clear();
            
            ConstructInfoPart();
            ConstructAutomatedTests();
        }
        
        private void RepopulateTests()
        {
            this.Remove(_allTestsScrollView);
            
            _testElements.Clear();
            _testGroupElements.Clear();
            _automatedTests.Clear();
            
            ConstructAutomatedTests();
        }

        private void ConstructInfoPart()
        {
            _validationInfoBox = new ValidationInfoElement();
            _pathBox = new PathBoxElement();

            var mainPath = ValidationState.Instance.ValidationStateData.SerializedMainPath;
            _pathBox.SetPathBoxValue(string.IsNullOrEmpty(mainPath) ? "Assets" : mainPath);
            
            var categorySelectionBox = new VisualElement();
            categorySelectionBox.AddToClassList("selection-box-row");
            
            VisualElement labelHelpRow = new VisualElement();
            labelHelpRow.AddToClassList("label-help-row");
            
            Label categoryLabel = new Label { text = "Category" };
            Image categoryLabelTooltip = new Image
            {
                tooltip = "Choose a base category of your package" +
                          "\n\nThis can be found in the Publishing Portal when creating the package listing or just " +
                          "selecting a planned one." +
                          "\n\nNote: Different categories could have different severities of several test cases."
            };
            
            labelHelpRow.Add(categoryLabel);
            labelHelpRow.Add(categoryLabelTooltip);
            
            _categoryMenu = new ToolbarMenu {name = "CategoryMenu"};
            _categoryMenu.AddToClassList("category-menu");
            PopulateCategoryDropdown();
            
            categorySelectionBox.Add(labelHelpRow);
            categorySelectionBox.Add(_categoryMenu);

            _validateButton = new Button(RunAllTests) {text = "Validate"};
            _validateButton.AddToClassList("run-all-button");

            _validationInfoBox.Add(categorySelectionBox);
            _validationInfoBox.Add(_pathBox);
            _validationInfoBox.Add(_validateButton);
            
            Add(_validationInfoBox);
        }

        private void ConstructAutomatedTests()
        {
            name = "AutomatedTests";

            _allTestsScrollView = new ScrollView
            {
                viewDataKey = "scrollViewKey",
            };
            _allTestsScrollView.AddToClassList("tests-scroll-view");

            _automatedTests = CreateAutomatedTestCases();
            var groupedTests = GroupTestsByStatus(_automatedTests);

            foreach (var status in StatusOrder)
            {
                var group = new AutomatedTestsGroupElement(status.ToString(), status, true);
                _testGroupElements.Add(status, group);
                _allTestsScrollView.Add(group);
                
                if (!groupedTests.ContainsKey(status))
                    continue;
                
                foreach (var test in groupedTests[status])
                {
                    var testElement = new AutomatedTestElement(test);
                    
                    _testElements.Add(test.Id, testElement);
                    group.AddTest(testElement);
                }
                
                if (StatusOrder[StatusOrder.Length - 1] != status)
                    group.AddSeparator();
            }

            Add(_allTestsScrollView);
        }

        private void PopulateCategoryDropdown()
        {
            var list = _categoryMenu.menu;
            list.AppendAction("None", _ => OnCategoryValueChange(string.Empty));

            _categoryEvaluator = new CategoryEvaluator(ValidationState.Instance.ValidationStateData.SerializedCategory);
            
            HashSet<string> categories = new HashSet<string>();
            var testData = ValidatorUtility.GetAutomatedTestCases(TestsPath, true);
            foreach (var test in testData)
            {
                AddCategoriesToSet(categories, test.WarningCategory);
                AddCategoriesToSet(categories, test.ErrorCategory);
            }

            foreach (var category in categories)
            {
                list.AppendAction(ConvertSlashToUnicodeSlash(category), _ => OnCategoryValueChange(category));
            }

            if (string.IsNullOrEmpty(_categoryEvaluator.GetCategory()))
                _categoryMenu.text = "Select Category";
            else
                _categoryMenu.text = _categoryEvaluator.GetCategory();
        }
        
        private string ConvertSlashToUnicodeSlash(string text)
        {
            return text.Replace('/', '\u2215');
        }

        private void AddCategoriesToSet(HashSet<string> set, ValidatorCategory category)
        {
            if (category == null)
                return;
            
            foreach (var filter in category.Filter)
                set.Add(filter);
        }
        
        private List<AutomatedTest> CreateAutomatedTestCases()
        {
            var testData = ValidatorUtility.GetAutomatedTestCases(TestsPath, true);
            var automatedTests = new List<AutomatedTest>();

            foreach (var t in testData)
            {
                var test = new AutomatedTest(t);

                var isTestApplicable = IsTestApplicableForContext(test);
                
                if(!isTestApplicable)
                    continue;

                if (!ValidationState.Instance.TestResults.ContainsKey(test.Id))
                    ValidationState.Instance.CreateTestContainer(test.Id);
                else
                    test.Result = ValidationState.Instance.TestResults[test.Id].Result;

                test.OnTestComplete += OnTestComplete;
                automatedTests.Add(test);
            }

            return automatedTests;
        }

        private Dictionary<TestResult.ResultStatus, List<AutomatedTest>> GroupTestsByStatus(List<AutomatedTest> tests)
        {
            var groupedDictionary = new Dictionary<TestResult.ResultStatus, List<AutomatedTest>>();
            
            foreach (var t in tests)
            {
                if (!groupedDictionary.ContainsKey(t.Result.Result))
                    groupedDictionary.Add(t.Result.Result, new List<AutomatedTest>());
                
                groupedDictionary[t.Result.Result].Add(t);
            }

            return groupedDictionary;
        }

        private async void RunAllTests()
        {
            ValidationState.Instance.SetMainPath(_pathBox.GetPathBoxValue());
            ValidationState.Instance.SetCategory(_categoryEvaluator.GetCategory());
            _validateButton.SetEnabled(false);

            // Make sure everything is collected and validation button is disabled
            await Task.Delay(100);

            for (int i = 0; i < _automatedTests.Count; i++)
            {
                var test = _automatedTests[i];

                EditorUtility.DisplayProgressBar("Validating", $"Running validation: {i + 1} - {test.Title}", (float)i / _automatedTests.Count);
                test.Run();
            }

            EditorUtility.ClearProgressBar();

            _validateButton.SetEnabled(true);
            ValidationState.Instance.SaveJson();
        }

        private void OnTestComplete(int id, TestResult result)
        {
            var testElement = _testElements[id];
            
            var currentStatus = _categoryEvaluator.Evaluate(testElement.GetAutomatedTest());
            result.Result = currentStatus;
            
            var lastStatus = testElement.GetLastStatus();

            if (_testGroupElements.ContainsKey(lastStatus) && _testGroupElements.ContainsKey(currentStatus))
            {
                if (lastStatus != currentStatus)
                {
                    _testGroupElements[lastStatus].RemoveTest(testElement);
                    _testGroupElements[currentStatus].AddTest(testElement);
                    
                    
                    testElement.GetAutomatedTest().Result = result;
                }
            }
            
            ValidationState.Instance.ChangeResult(id, result);
            testElement.ResultChanged();
        }
        
        private void OnCategoryValueChange(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                _categoryMenu.text = "Select Category";
                _categoryEvaluator.SetCategory(string.Empty);
            }
            else
            {
                _categoryMenu.text = value;
                _categoryEvaluator.SetCategory(value);
            }
            
            RepopulateTests();
        }

        private bool IsTestApplicableForContext(AutomatedTest test)
        {
            var selectedCategory = _categoryEvaluator.GetCategory();
            
            if (string.IsNullOrEmpty(selectedCategory))
                return true;
            
            return test.IsApplicableToAnySeverity(selectedCategory);
        }
    }
}