using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetStoreTools.Validator
{
    [CustomEditor(typeof(ValidationTestScriptableObject), true)]
    internal class ValidationTestScriptableObjectInspector : UnityEditor.Editor
    {
        private ValidationTestScriptableObject _data;
        private ValidationTestScriptableObject[] _allObjects;
        
        private SerializedProperty _script;

        private SerializedProperty _errorCategory;
        private SerializedProperty _warningCategory;

        private string[] _allMethodNames;
        private bool _hadChanges;
        
        private void OnEnable()
        {
            if (target == null) return;
            
            _data = target as ValidationTestScriptableObject;

            _script = serializedObject.FindProperty("m_Script");
            
            _errorCategory = serializedObject.FindProperty(nameof(ValidationTestScriptableObject.ErrorCategory));
            _warningCategory = serializedObject.FindProperty(nameof(ValidationTestScriptableObject.WarningCategory));
            
            _allObjects = ValidatorUtility.GetAutomatedTestCases("Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/", true);
            _allMethodNames = TestActions.Instance.GetType().GetMethods().Where(x => x.DeclaringType == typeof(TestActions)).Select(x => x.Name).ToArray();
            _hadChanges = false;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField(GetInspectorTitle(), new GUIStyle(EditorStyles.centeredGreyMiniLabel) {fontSize = 24}, GUILayout.MinHeight(50));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(_script);

            EditorGUI.BeginChangeCheck();
            // ID field
            EditorGUILayout.IntField("Test Id", _data.Id);
            if (!ValidateID())
                EditorGUILayout.HelpBox("ID is already in use", MessageType.Warning);
            EditorGUI.EndDisabledGroup();

            // Other fields
            _data.Title = EditorGUILayout.TextField("Title", _data.Title);
            if (string.IsNullOrEmpty(_data.Title))
                EditorGUILayout.HelpBox("Title cannot be empty", MessageType.Warning);
            
            EditorGUILayout.LabelField("Description");
            GUIStyle myTextAreaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            _data.Description = EditorGUILayout.TextArea(_data.Description, myTextAreaStyle);
            
            EditorGUILayout.PropertyField(_errorCategory, new GUIContent("Error Category"));
            EditorGUILayout.PropertyField(_warningCategory, new GUIContent("Warning Category"));
            
            // Test Method field
            if (!ContainsMethod() && !string.IsNullOrEmpty(_data.Title))
            {
                EditorGUILayout.LabelField("Test Method Name", ConstructTestMethodName());
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Generate Test Method Stub", GUILayout.MaxWidth(200f)))
                {
                    _data.TestMethodName = ConstructTestMethodName();
                    ValidatorUtility.GenerateTestMethodStub(_data);
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LabelField("Test Method Name", _data.TestMethodName);
                EditorGUI.EndDisabledGroup();
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(target);
                _hadChanges = true;
            }

            _hadChanges = serializedObject.ApplyModifiedProperties() || _hadChanges;
        }
        
        private string GetInspectorTitle()
        {
            switch (_data)
            {
                case AutomatedTestScriptableObject _:
                    return "Automated Test";
                default:
                    return "Miscellaneous Test";
            }
        }

        private bool ValidateID()
        {
            return !_allObjects.Any(x => x.Id == _data.Id && x != _data);
        }

        private bool ContainsMethod()
        {
            if (string.IsNullOrEmpty(_data.TestMethodName))
                return false;
            
            return _allMethodNames.Any(a => a == _data.TestMethodName);
        }

        private string ConstructTestMethodName()
        {
            return Regex.Replace("_" + _data.Id + "_" + _data.Title, "[^A-Za-z0-9-_]", "");
        }

        private void OnDisable()
        {
            if (!_hadChanges) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}