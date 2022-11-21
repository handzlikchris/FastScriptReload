using System.Collections.Generic;
using UnityEditor;
using System.Linq;
using System.IO;

namespace AssetStoreTools.Validator
{
    internal static class ValidatorUtility
    {
        private const string TestActionsPath = "Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/Scripts/TestActions.cs";

        public static ValidationTestScriptableObject[] GetAutomatedTestCases(string path, bool sortById)
        {
            string[] guids = AssetDatabase.FindAssets("t:AutomatedTestScriptableObject", new[] { path });
            ValidationTestScriptableObject[] tests = new ValidationTestScriptableObject[guids.Length];
            for (int i = 0; i < tests.Length; i++)
            {
                string testPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                AutomatedTestScriptableObject test = AssetDatabase.LoadAssetAtPath<AutomatedTestScriptableObject>(testPath);

                tests[i] = test;
            }

            if (sortById)
                tests = tests.OrderBy(x => x.Id).ToArray();

            return tests;
        }

        public static void GenerateTestMethodStub(ValidationTestScriptableObject data)
        {
            string[] scriptLines = File.ReadAllLines(TestActionsPath);
            scriptLines = scriptLines.Reverse().ToArray();

            int startingLine = -1;
            int indentationLevel = 0; // 0 = In Nothing, 1 = In Namespace, 2 = In Class, 3 = In Method

            for (int i = 0; i < scriptLines.Length; i++)
            {
                string line = scriptLines[i];
                line = line.Trim();

                if (indentationLevel == 2)
                {
                    startingLine = i;
                    break;
                }

                switch (line.Length)
                {
                    case 0:
                        continue;
                    case 1:
                        {
                            if (line[0] == '{')
                                indentationLevel--;
                            if (line[0] == '}')
                                indentationLevel++;
                            continue;
                        }
                }
            }

            List<string> methodStub = new List<string>();
            string indentationSignature = new string('\t', indentationLevel);
            string indentationBody = new string('\t', indentationLevel + 1);
            string methodParams = "";
            string methodBody = $"{indentationBody}TestResult result = new TestResult();\r\n";
            methodBody += $"{indentationBody}return result;";

            string methodSignature = $"{indentationSignature}public TestResult {data.TestMethodName}({methodParams})";

            methodStub.Add(indentationSignature + "}");
            methodStub.Add(methodBody);
            methodStub.Add(indentationSignature + "{");
            methodStub.Add(methodSignature);
            methodStub.Add(string.Empty);

            string[] newScriptLines = new string[scriptLines.Length + methodStub.Count];
            for (int i = 0; i < startingLine; i++)
            {
                newScriptLines[i] = scriptLines[i];
            }
            for (int i = 0; i < methodStub.Count; i++)
            {
                newScriptLines[startingLine + i] = methodStub[i];
            }
            for (int i = 0; i < scriptLines.Length - startingLine; i++)
            {
                newScriptLines[startingLine + methodStub.Count + i] = scriptLines[i + startingLine];
            }

            newScriptLines = newScriptLines.Reverse().ToArray();

            File.WriteAllLines(TestActionsPath, newScriptLines);
            AssetDatabase.Refresh();
        }
    }
}