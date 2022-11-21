using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace AssetStoreTools.Validator
{
    [Serializable]
    internal class ValidationStateData
    {
        public string SerializedMainPath;
        public string SerializedCategory;
        public List<int> SerializedKeys;
        public List<TestResultData> SerializedValues;
    }
        
    [Serializable]
    internal class TestResultData
    {
        public TestResult Result;
    }

    internal class ValidationState
    {
        private const string ValidationDataFilename = "AssetStoreValidationState.asset";
        private const string PersistentDataLocation = "Library";

        public Dictionary<int, TestResultData> TestResults = new Dictionary<int, TestResultData>();
        public ValidationStateData ValidationStateData;
        public Action OnJsonSave;

        private static ValidationState s_instance;
        public static ValidationState Instance
        {
            get
            {
                if (s_instance == null)
                    s_instance = new ValidationState();

                s_instance.LoadJson();

                return s_instance;
            }
        }

        private void LoadJson()
        {
            if (s_instance.TestResults.Count != 0)
                return;
            
            var saveFile = $"{PersistentDataLocation}/{ValidationDataFilename}";

            if (!File.Exists(saveFile))
            {
                s_instance.ValidationStateData = new ValidationStateData
                {
                    SerializedMainPath = "Assets",
                    SerializedCategory = ""
                };
                return;
            }
            
            var fileContents = File.ReadAllText(saveFile);
            var data = JsonUtility.FromJson<ValidationStateData>(fileContents);
            s_instance.ValidationStateData = data;

            for (var i = 0; i < s_instance.ValidationStateData.SerializedKeys.Count; i++)
            {
                s_instance.TestResults.Add(s_instance.ValidationStateData.SerializedKeys[i], s_instance.ValidationStateData.SerializedValues[i]);
            }
        }

        public void SaveJson()
        {
            var saveFile = $"{PersistentDataLocation}/{ValidationDataFilename}";

            if (TestResults.Keys.Count == 0)
                return;

            ValidationStateData.SerializedKeys = TestResults.Keys.ToList();
            ValidationStateData.SerializedValues = TestResults.Values.ToList();
            
            var jsonString = JsonUtility.ToJson(ValidationStateData);

            File.WriteAllText(saveFile, jsonString);
            
            OnJsonSave?.Invoke();
        }

        public void CreateTestContainer(int testId)
        {
            s_instance.TestResults.Add(testId, new TestResultData());
        }

        public void ChangeResult(int index, TestResult result)
        {
            if (!s_instance.TestResults.ContainsKey(index))
                CreateTestContainer(index);
            
            s_instance.TestResults[index].Result = result;
        }

        public void SetMainPath(string path)
        {
            s_instance.ValidationStateData.SerializedMainPath = path;
        }

        public void SetCategory(string category)
        {
            s_instance.ValidationStateData.SerializedCategory = category;
        }
        
    }
}