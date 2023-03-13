using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastScriptReload.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FastScriptReload.Editor.Compilation.ScriptGenerationOverrides
{
    [InitializeOnLoad]
    public static class ScriptGenerationOverridesManager
    {
        private static float LoadOverridesFolderFilesEveryNSeconds = 5;
        
        private static readonly string TemplateInterfaceDeclaration = @"

//New interface declaration, this is very useful in cases where code depends on some internal interfaces that re-compiled code can no longer access. Simply define them here and code will compile.
//You can add any type in that manner
public interface ITestNewInterface {
    bool Test { get; set; }
}";

        private static readonly string TemplateTopComment = @"// You can use this file to specify custom code overrides. Those will be applied to resulting code.
// This approach is very useful if your code is failing to compile due to one of the existing limitations.
// 
//  While I work on reducing limitations you can simply specify override with proper code to make sure you can continue working.
// 
// 1) Simply define code with same structure as your original class, make sure to include any namespace.
// 2) Rename classes and types to have '<ClassPostfix>' postfix.
// 
// eg. 'MyClassName' needs to be changed to MyClassName<ClassPostfix> otherwise it won't be properly connected.
//
// 3) Add any methods that you want to override, using same method signature. Whole method body will be replaced and no code adjustments will be run on it.
// 4) You can add any additional types - this is quite useful when you hit limitation with internal interfaces - where compiler can not access them due to protection modifiers. 
// You can simply redefine those here, while not ideal it'll allow you to continue using Hot-Reload without modifying your code.
// 
// Tool will now attempt to create a template file for you with first found class and first method as override, please adjust as necessary. 
// It'll also create an example redefined interface.
// If you can't see anything please refer to the above and create overrides file manually.
// 
// You can also refer to documentation section 'User defined script rewrite overrides'
";

        public static DirectoryInfo UserDefinedScriptRewriteOverridesFolder { get; }
        private static double _lastTimeOverridesFolderFilesRead;

        public static List<UserDefinedScriptOverride> UserDefinedScriptOverrides { get; } = new List<UserDefinedScriptOverride>();

        static ScriptGenerationOverridesManager()
        {
            //TODO: allow to customize later from code, eg for user that'd like to include in source control
            UserDefinedScriptRewriteOverridesFolder = new DirectoryInfo(Application.persistentDataPath + @"\FastScriptReload\ScriptOverrides");
            UpdateUserDefinedScriptOverridesFileCache();
            EditorApplication.update += Update;
        }

        private static void Update()
        {
            var timeSinceStartup = EditorApplication.timeSinceStartup;
            if (_lastTimeOverridesFolderFilesRead + LoadOverridesFolderFilesEveryNSeconds < timeSinceStartup)
            {
                _lastTimeOverridesFolderFilesRead = timeSinceStartup;

                UpdateUserDefinedScriptOverridesFileCache();
            }
        }

        private static void UpdateUserDefinedScriptOverridesFileCache()
        {
            UserDefinedScriptOverrides.Clear();
            if (UserDefinedScriptRewriteOverridesFolder.Exists)
            {
                UserDefinedScriptOverrides.AddRange(UserDefinedScriptRewriteOverridesFolder.GetFiles().Select(f => new UserDefinedScriptOverride(f)));
            }
        }

        public static void AddScriptOverride(MonoScript script)
        {
            EnsureOverrideFolderExists();

            var overridenFile = new FileInfo(Path.Combine(UserDefinedScriptRewriteOverridesFolder.FullName, script.name + ".cs"));
            if (!overridenFile.Exists)
            {
                var originalFile = new FileInfo(Path.Combine(Path.Combine(Application.dataPath + "//..", AssetDatabase.GetAssetPath(script))));

                var templateString = string.Empty;
                try
                {
                    var fileCode = File.ReadAllText(originalFile.FullName);
                    var tree = CSharpSyntaxTree.ParseText(fileCode, new CSharpParseOptions(preprocessorSymbols: DynamicCompilationBase.ActiveScriptCompilationDefines));
                    var root = tree.GetRoot();

                    var firstType = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
                    if (firstType != null)
                    {
                        var members = new SyntaxList<MemberDeclarationSyntax>();
                        var firstMethod = firstType.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Body != null);
                        if (firstMethod != null)
                        {
                            var block = SyntaxFactory.Block();
                            block = block.AddStatements(SyntaxFactory.EmptyStatement().WithLeadingTrivia(
                                SyntaxFactory.Comment(@"/* Any code will be replaced with original method of same signature in same type*/"))
                            );
                            firstMethod = firstMethod
                                .WithBody(block)
                                .WithTriviaFrom(firstMethod);
                            members = members.Add(firstMethod);
                        }
                        
                        root = root.ReplaceNode(firstType, firstType
                            .ReplaceToken(
                                firstType.Identifier, 
                                SyntaxFactory.Identifier(firstType.Identifier.ValueText + AssemblyChangesLoader.ClassnamePatchedPostfix)
                            )
                            .WithMembers(members)).NormalizeWhitespace();

                        var interfaceDeclaration = CSharpSyntaxTree.ParseText(TemplateInterfaceDeclaration);
                        
                        root = ((CompilationUnitSyntax)root).AddMembers(
                            interfaceDeclaration.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>().First()
                        );
                    }

                    templateString = root.ToFullString();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to generate user defined script override template from your file, please refer to note at the start of the file. {e}");
                }

                if (!overridenFile.Exists)
                {
                    File.WriteAllText(overridenFile.FullName, 
                        TemplateTopComment.Replace("<ClassPostfix>", AssemblyChangesLoader.ClassnamePatchedPostfix) + templateString
                    );
                    UpdateUserDefinedScriptOverridesFileCache();
                }
            }
            
            InternalEditorUtility.OpenFileAtLineExternal(overridenFile.FullName, 0);
        }
        
        public static bool TryRemoveScriptOverride(MonoScript originalScript)
        {
            EnsureOverrideFolderExists();

            var overridenFile = new FileInfo(Path.Combine(UserDefinedScriptRewriteOverridesFolder.FullName, originalScript.name + ".cs"));
            if (overridenFile.Exists)
            {
                return TryRemoveScriptOverride(overridenFile);
            }

            return false;
        }

        private static bool TryRemoveScriptOverride(FileInfo overridenFile)
        {
            try
            {
                overridenFile.Delete();
                UpdateUserDefinedScriptOverridesFileCache();
            }
            catch (Exception)
            {
                Debug.Log($"Unable to remove: '{overridenFile.Name}' - make sure it's not locked / open in editor");
                throw;
            }

            return true;
        }

        public static bool TryRemoveScriptOverride(UserDefinedScriptOverride scriptOverride)
        {
            return TryRemoveScriptOverride(scriptOverride.File);
        }

        public static bool TryGetScriptOverride(FileInfo changedFile, out FileInfo overridesFile)
        {
            overridesFile = UserDefinedScriptOverrides.FirstOrDefault(f => f.File.Name == changedFile.Name && f.File.Exists)?.File;
            return overridesFile?.Exists ?? false;
        }
        
        private static void EnsureOverrideFolderExists()
        {
            if (!UserDefinedScriptRewriteOverridesFolder.Exists)
                UserDefinedScriptRewriteOverridesFolder.Create();
        }
    }

    public class UserDefinedScriptOverride
    {
        public FileInfo File { get; }

        public UserDefinedScriptOverride(FileInfo file)
        {
            File = file;
        }
    }
}