using System;
using System.Collections.Generic;
using ImmersiveVRTools.Editor.Common.WelcomeScreen;
using ImmersiveVRTools.Editor.Common.WelcomeScreen.GuiElements;
using ImmersiveVRTools.Editor.Common.WelcomeScreen.PreferenceDefinition;
using ImmersiveVRTools.Editor.Common.WelcomeScreen.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class FastScriptReloadWelcomeScreen : ProductWelcomeScreenBase
{
    public static string BaseUrl = "https://immersivevrtools.com";
    public static string GenerateGetUpdatesUrl(string userId, string versionId)
    {
        return $"{BaseUrl}/updates/fast-script-reload/{userId}?CurrentVersion={versionId}";
    }
    public static string VersionId = "1.0";
    private static readonly string ProjectIconName = "ProductIcon64";
    public static readonly string ProjectName = "fast-script-reload";

    private static Vector2 _WindowSizePx = new Vector2(650, 500);
    private static string _WindowTitle = "Fast Script Reload";

    public static ChangeMainViewButton ExclusionsSecion { get; private set; }

    public void OpenExclusionsSection()
    {
        ExclusionsSecion.OnClick(this);
    }

    private static readonly List<GuiSection> LeftSections = new List<GuiSection>() {
        new GuiSection("", new List<ClickableElement>
        {
            new LastUpdateButton("New Update!", (screen) => LastUpdateUpdateScrollViewSection.RenderMainScrollViewSection(screen)),
            new ChangeMainViewButton("Welcome", (screen) => MainScrollViewSection.RenderMainScrollViewSection(screen)),
        }),
        new GuiSection("Options", new List<ClickableElement>
        {
            new ChangeMainViewButton("Reload", (screen) =>
            {
                const int sectionBreakHeight = 15;
                GUILayout.Label(
                    @"Asset watches all script files and automatically hot-reloads on change, you can disable that behaviour and reload on demand.",
                    screen.TextStyle
                );
                
                using (LayoutHelper.LabelWidth(300))
                {
                    ProductPreferenceBase.RenderGuiAndPersistInput(FastScriptReloadPreference.EnableAutoReloadForChangedFiles);
                }
                GUILayout.Space(sectionBreakHeight);
                
                EditorGUILayout.HelpBox("On demand reload:\r\nvia Window -> Fast Script Reload -> Force Reload, \r\nor by calling 'FastScriptIterationManager.Instance.TriggerReloadForChangedFiles()'", MessageType.Info);
                GUILayout.Space(sectionBreakHeight);
                
                GUILayout.Label(
                    @"For performance reasons script changes are batched are reloaded every N seconds",
                    screen.TextStyle
                );

                using (LayoutHelper.LabelWidth(300))
                {
                    ProductPreferenceBase.RenderGuiAndPersistInput(FastScriptReloadPreference.BatchScriptChangesAndReloadEveryNSeconds);
                }

                GUILayout.Space(sectionBreakHeight);
            }),
            (ExclusionsSecion = new ChangeMainViewButton("Exclusions", (screen) => 
            {
                EditorGUILayout.HelpBox("Those are easiest to manage from Project window by right clicking on script file and selecting: " +
                                        "\r\nFast Script Reload -> Add Hot-Reload Exclusion " +
                                        "\r\nFast Script Reload -> Remove Hot-Reload Exclusion", MessageType.Info);
                GUILayout.Space(10);
                
                ProductPreferenceBase.RenderGuiAndPersistInput(FastScriptReloadPreference.FilesExcludedFromHotReload);
            })),
            // new ChangeMainViewButton("Network", (screen) => //TODO: add for networked version, with compilation symbol?
            // {
            //     
            // })
        }),
        new GuiSection("Launch Demo", new List<ClickableElement>
        {
            new LaunchSceneButton("Basic Example", (s) => GetScenePath("ExampleScene"), (screen) =>
            {
                GUILayout.Label(
@"Asset is very simple to use:

1) Hit play to start.
2) Go to 'FunctionLibrary.cs' (Assets/QuickCodeIteration/Examples/Scripts/)
3) Change 'Ripple' method (eg change line before return statement to 'p.z = v * 10'
4) Save file
5) See change immediately",
                    screen.TextStyle
                );
                
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("There are some limitations to what can be Hot-Reloaded, documentation lists them under 'limitations' section.", MessageType.Warning);
            })
        })
    };

    private static readonly string RedirectBaseUrl = "https://immersivevrtools.com/redirect/immersive-vr-mechanic-tools"; 
    private static readonly GuiSection TopSection = new GuiSection("Support", new List<ClickableElement>
        {
            new OpenUrlButton("Documentation", $"{RedirectBaseUrl}/documentation"),
            new OpenUrlButton("Unity Forum", $"{RedirectBaseUrl}/unity-forum"),
            new OpenUrlButton("Contact", $"{RedirectBaseUrl}/contact")
        }
    );

    private static readonly GuiSection BottomSection = new GuiSection(
        "I want to make this tool better. And I need your help!",
        $"It'd be great if you could share your feedback (good and bad) with me. I'm very keen to make this tool better and that can only happen with your help. Please use:",
        new List<ClickableElement>
        {
            new OpenUrlButton(" Unity Forum", $"{RedirectBaseUrl}/unity-forum"),
            new OpenUrlButton(" or Write a Short Review", $"{RedirectBaseUrl}/asset-store-review"),
        }
    );

    private static readonly ScrollViewGuiSection MainScrollViewSection = new ScrollViewGuiSection(
        "", (screen) =>
        {
            GenerateCommonWelcomeText(FastScriptReloadPreference.ProductName, screen);

            GUILayout.Label("Quick adjustments:", screen.LabelStyle);
            using (LayoutHelper.LabelWidth(300))
            {
                ProductPreferenceBase.RenderGuiAndPersistInput(FastScriptReloadPreference.BatchScriptChangesAndReloadEveryNSeconds);
                ProductPreferenceBase.RenderGuiAndPersistInput(FastScriptReloadPreference.EnableAutoReloadForChangedFiles);
            }
        }
    );

    private static readonly ScrollViewGuiSection LastUpdateUpdateScrollViewSection = new ScrollViewGuiSection(
        "New Update", (screen) =>
        {
            GUILayout.Label(screen.LastUpdateText, screen.BoldTextStyle, GUILayout.ExpandHeight(true));
        }
    );

    public override string WindowTitle { get; } = _WindowTitle;
    public override Vector2 WindowSizePx { get; } = _WindowSizePx;


    [MenuItem("Window/Fast Script Reload/Start Screen", false, 1999)]
    public static FastScriptReloadWelcomeScreen Init()
    {
        return OpenWindow<FastScriptReloadWelcomeScreen>(_WindowTitle, _WindowSizePx);
    }
    
    [MenuItem("Window/Fast Script Reload/Force Reload", true, 1999)]
    public static bool ForceReloadValidate()
    {
        return EditorApplication.isPlaying;
    }
    
    [MenuItem("Window/Fast Script Reload/Force Reload", false, 1999)]
    public static void ForceReload()
    {
        QuickCodeIterationManager.Instance.TriggerReloadForChangedFiles();
    }

    public void OnEnable()
    {
        OnEnableCommon(ProjectIconName);
    }

    public void OnGUI()
    {
        RenderGUI(LeftSections, TopSection, BottomSection, MainScrollViewSection);
    }
}

public class FastScriptReloadPreference : ProductPreferenceBase
{
    public static string BuildSymbol_LoadAssemblyChangesOverNetwork = "QuickCodeIteration_LoadAssemblyOverNetwork_Enabled"; //TODO: wire up

    public const string ProductName = "Fast Script Reload";
    private static string[] ProductKeywords = new[] { "productivity", "tools" };

    public static readonly IntProjectEditorPreferenceDefinition BatchScriptChangesAndReloadEveryNSeconds = new IntProjectEditorPreferenceDefinition(
        "Batch script changes and reload every N seconds", "BatchScriptChangesAndReloadEveryNSeconds", 3);

    public static readonly ToggleProjectEditorPreferenceDefinition EnableAutoReloadForChangedFiles = new ToggleProjectEditorPreferenceDefinition(
        "Enable auto Hot-Reload for changed files", "EnableAutoReloadForChangedFiles", true);
    
    public static readonly StringListProjectEditorPreferenceDefinition FilesExcludedFromHotReload = new StringListProjectEditorPreferenceDefinition(
        "Files excluded from Hot-Reload", "FilesExcludedFromHotReload", new List<string> {}, isReadonly: true);


    //TODO: ensure URP / default shaders are set depending on project (for example scene)
    public static void SetCommonMaterialsShader(ShadersMode newShaderModeValue)
    {
        var rootToolFolder = AssetPathResolver.GetAssetFolderPathRelativeToScript(ScriptableObject.CreateInstance(typeof(FastScriptReloadWelcomeScreen)), 1);
        var assets = AssetDatabase.FindAssets("t:Material", new[] { rootToolFolder });

        try
        {
            Shader shaderToUse = null;
            switch (newShaderModeValue)
            {
                case ShadersMode.HDRP: shaderToUse = Shader.Find("HDRP/Lit"); break;
                case ShadersMode.URP: shaderToUse = Shader.Find("Universal Render Pipeline/Lit"); break;
                case ShadersMode.Surface: shaderToUse = Shader.Find("Standard"); break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var guid in assets)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (material.shader.name != shaderToUse.name)
                {
                    material.shader = shaderToUse;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Shader does not exist: {ex.Message}");
        }
    }

    public static List<ProjectEditorPreferenceDefinitionBase> PreferenceDefinitions = new List<ProjectEditorPreferenceDefinitionBase>()
    {
        CreateDefaultShowOptionPreferenceDefinition(),
        BatchScriptChangesAndReloadEveryNSeconds,
        EnableAutoReloadForChangedFiles
    };

    private static bool PrefsLoaded = false;



#if UNITY_2019_1_OR_NEWER
    [SettingsProvider]
    public static SettingsProvider ImpostorsSettings()
    {
        return GenerateProvider(ProductName, ProductKeywords, PreferencesGUI);
    }

#else
	[PreferenceItem(ProductName)]
#endif
    public static void PreferencesGUI()
    {
        if (!PrefsLoaded)
        {
            LoadDefaults(PreferenceDefinitions);
            PrefsLoaded = true;
        }

        RenderGuiCommon(PreferenceDefinitions);
    }

    public enum ShadersMode
    {
        HDRP,
        URP,
        Surface
    }
}

[InitializeOnLoad]
public class FastScriptReloadWelcomeScreenInitializer : WelcomeScreenInitializerBase
{
    static FastScriptReloadWelcomeScreenInitializer()
    {
        var userId = ProductPreferenceBase.CreateDefaultUserIdDefinition(FastScriptReloadWelcomeScreen.ProjectName).GetEditorPersistedValueOrDefault().ToString();

        HandleUnityStartup(
            () => FastScriptReloadWelcomeScreen.Init(),
            FastScriptReloadWelcomeScreen.GenerateGetUpdatesUrl(userId, FastScriptReloadWelcomeScreen.VersionId),
            new List<ProjectEditorPreferenceDefinitionBase>(),
            (isFirstRun) =>
            {
                AutoDetectAndSetShaderMode();
            }
        );
    }

    private static void AutoDetectAndSetShaderMode()
    {
        var usedShaderMode = FastScriptReloadPreference.ShadersMode.Surface;
        var renderPipelineAsset = GraphicsSettings.renderPipelineAsset;
        if (renderPipelineAsset == null)
        {
            usedShaderMode = FastScriptReloadPreference.ShadersMode.Surface;
        }
        else if (renderPipelineAsset.GetType().Name.Contains("HDRenderPipelineAsset"))
        {
            usedShaderMode = FastScriptReloadPreference.ShadersMode.HDRP;
        }
        else if (renderPipelineAsset.GetType().Name.Contains("UniversalRenderPipelineAsset"))
        {
            usedShaderMode = FastScriptReloadPreference.ShadersMode.URP;
        }
        
        FastScriptReloadPreference.SetCommonMaterialsShader(usedShaderMode);
    }
}