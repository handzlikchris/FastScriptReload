using UnityEditor;
using UnityEngine.UIElements;

namespace AssetStoreTools.Uploader
{
    internal static class StyleSelector
    {
        private const string StylesPath = "Packages/com.unity.asset-store-tools/Editor/AssetStoreUploader/Styles/";

        private const string BaseWindowMain = "Base/BaseWindow_Main.uss";
        private const string BaseWindowLight = "Base/BaseWindow_Light.uss";
        private const string BaseWindowDark = "Base/BaseWindow_Dark.uss";

        private const string LoginWindowMain = "Login/Login_Main.uss";
        private const string LoginWindowLight = "Login/Login_Light.uss";
        private const string LoginWindowDark = "Login/Login_Dark.uss";

        private const string UploadWindowMain = "Upload/UploadWindow_Main.uss";
        private const string UploadWindowLight = "Upload/UploadWindow_Light.uss";
        private const string UploadWindowDark = "Upload/UploadWindow_Dark.uss";

        private const string AllPackagesWindowMain = "Upload/AllPackages/AllPackages_Main.uss";
        private const string AllPackagesWindowLight = "Upload/AllPackages/AllPackages_Light.uss";
        private const string AllPackagesWindowDark = "Upload/AllPackages/AllPackages_Dark.uss";

        public enum Style
        {
            Base,
            Login,
            UploadWindow,
            AllPackages,
        }

        public static void SetStyle(VisualElement element, Style style, bool isLightTheme)
        {
            string stylePath = StylesPath, themePath = StylesPath;

            switch (style)
            {
                case Style.Base:
                    stylePath += BaseWindowMain;
                    themePath += isLightTheme ? BaseWindowLight : BaseWindowDark;
                    break;
                case Style.Login:
                    stylePath += LoginWindowMain;
                    themePath += isLightTheme ? LoginWindowLight : LoginWindowDark;
                    break;
                case Style.UploadWindow:
                    stylePath += UploadWindowMain;
                    themePath += isLightTheme ? UploadWindowLight : UploadWindowDark;
                    break;
                case Style.AllPackages:
                    stylePath += AllPackagesWindowMain;
                    themePath += isLightTheme ? AllPackagesWindowLight : AllPackagesWindowDark;
                    break;
            }

            var styleAsset = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylePath);
            var themeAsset = AssetDatabase.LoadAssetAtPath<StyleSheet>(themePath);

            element.styleSheets.Add(styleAsset);
            element.styleSheets.Add(themeAsset);
        }
    }
}