using System;
using System.Threading.Tasks;

namespace AssetStoreTools.Validator
{
    internal abstract class ValidationTest
    {
        public int Id;
        public string Title;
        public string Description;
        public string TestMethodName;
        
        public ValidatorCategory ErrorCategory;
        public ValidatorCategory WarningCategory;
        
        public TestResult Result;

        public event Action<int, TestResult> OnTestComplete;

        protected ValidationTest(ValidationTestScriptableObject source)
        {
            Id = source.Id;
            Title = source.Title;
            Description = source.Description;
            TestMethodName = source.TestMethodName;
            ErrorCategory = source.ErrorCategory;
            WarningCategory = source.WarningCategory;
            Result = new TestResult();
        }

        public abstract void Run();

        protected void OnTestCompleted()
        {
            OnTestComplete?.Invoke(Id, Result);
        }

        public bool IsApplicableToAnySeverity(string category)
        {
            bool appliesToError = ErrorCategory.AppliesToCategory(category);
            bool appliesToWarning = WarningCategory.AppliesToCategory(category);
            return appliesToError || appliesToWarning;
        }
        
        public bool IsApplicableToAnySeveritySlugified(string category)
        {
            bool appliesToError = ErrorCategory.AppliesToCategory(Slugify(category));
            bool appliesToWarning = WarningCategory.AppliesToCategory(Slugify(category));
            return appliesToError || appliesToWarning;
        }
        
        public string Slugify(string value)
        {
            string newValue = value.Replace(' ', '-').ToLower();
            return newValue;
        }
    }
}
