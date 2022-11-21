using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssetStoreTools.Validator
{
    internal class AssetStoreValidation : AssetStoreToolsWindow
    {
        protected override string WindowTitle => "Asset Store Validator";
        
        private const string StylesPath = "Packages/com.unity.asset-store-tools/Editor/AssetStoreValidator/Styles";
        private const string MainStylesName = "AssetStoreValidator_Main";
        private const string DarkStylesName = "AssetStoreValidator_Dark";
        private const string LightStylesName = "AssetStoreValidator_Light";

        public static Action OnWindowDestroyed;
        
        private AutomatedTestsGroup _automatedTestsGroup;

        protected override void Init()
        {
            minSize = new Vector2(350, 350);

            base.Init();
            this.SetAntiAliasing(4);

            VisualElement root = rootVisualElement;
            
            root.AddToClassList("root");

            // Clean it out, in case the window gets initialized again
            root.Clear();

            // Getting a reference to the USS Document and adding stylesheet to the root
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesPath}/{MainStylesName}.uss");
            root.styleSheets.Add(styleSheet);

            var toolSkinName = LightStylesName;
            if (EditorGUIUtility.isProSkin)
                toolSkinName = DarkStylesName;
            
            var coloredStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{StylesPath}/{toolSkinName}.uss");
            root.styleSheets.Add(coloredStyleSheet);

            ConstructWindow();
        }

        private void ConstructWindow()
        {
            _automatedTestsGroup = new AutomatedTestsGroup();
            rootVisualElement.Add(_automatedTestsGroup);
        }

        private void OnDestroy()
        {
            OnWindowDestroyed?.Invoke();
        }
    }
}