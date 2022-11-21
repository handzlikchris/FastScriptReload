namespace AssetStoreTools.Validator
{
    internal class CategoryEvaluator
    {
        private string _category;
        
        public CategoryEvaluator(string category)
        {
            _category = category;
        }

        public void SetCategory(string category)
        {
            _category = category;
        }

        public string GetCategory()
        {
            return _category;
        }
        
        public TestResult.ResultStatus Evaluate(ValidationTest validation, bool slugify = false)
        {
            var result = validation.Result.Result;
            if (result != TestResult.ResultStatus.Fail && result != TestResult.ResultStatus.Warning) 
                return result;
            
            var category = _category;
                
            if (slugify)
                category = validation.Slugify(category);
                
            // Error category check
            if (validation.ErrorCategory.AppliesToCategory(category))
                return TestResult.ResultStatus.Fail;
                
            // Warning category check
            if (validation.WarningCategory.AppliesToCategory(category))
                return TestResult.ResultStatus.Warning;

            // Did not apply to any category, return the original result
            return result;
        }
        
        // Used by ab-builder
        public TestResult.ResultStatus EvaluateAndSlugify(ValidationTest validation)
        {
            return Evaluate(validation, true);
        }
    }
}