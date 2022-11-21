using UnityEngine;

namespace AssetStoreTools.Validator
{
    internal class ValidationTestScriptableObject : ScriptableObject
    {
        [SerializeField, HideInInspector]
        private bool HasBeenInitialized;

        public int Id;
        public string Title;
        public string Description;
        public string TestMethodName;

        public ValidatorCategory ErrorCategory;
        public ValidatorCategory WarningCategory;
        
        private void OnEnable()
        {
            // To do: maybe replace with Custom Inspector
            if (HasBeenInitialized) 
                return;
            
            var existingTestCases = ValidatorUtility.GetAutomatedTestCases("Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/Tests/", true);
            if (existingTestCases.Length > 0)
                Id = existingTestCases[existingTestCases.Length - 1].Id + 1;
            else
                Id = 1;
            HasBeenInitialized = true;
        }
    }
}