using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FastScriptReload.Editor.Compilation;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Runtime.Common;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

namespace FastScriptReload.Tests.Editor.Integration.CodePatterns
{
    public delegate void AfterDetourTest(CompileResult compileResult);
    
    public abstract class CompileWithRedirectTestBase : IPrebuildSetup
    {
        protected static void TestCompileAndDetour(string filePath)
            => TestCompileAndDetour(filePath, (compileResult) => { });
        
        protected static void TestCompileAndDetour(string filePath, AfterDetourTest afterDetourTest)
        {
            var originalSourceCode = File.ReadAllText(filePath);
            try
            {
                var adjustedSourceCode = originalSourceCode.Replace(TestDetourConfirmation.MockRuntimeCodeChange, string.Empty);

                try
                {
                    File.WriteAllText(filePath, adjustedSourceCode);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to adjust code file (test), make sure it's not locked in IDE. '{filePath}', {e}");
                    throw e;
                }


                var dynamicallyLoadedAssemblyCompilerResult = CompileCode(filePath);

                var assemblyChangesLoader = AssemblyChangesLoaderResolver.Instance.Resolve();
                var options = new AssemblyChangesLoaderEditorOptionsNeededInBuild(true, false);
                assemblyChangesLoader.DynamicallyUpdateMethodsForCreatedAssembly(dynamicallyLoadedAssemblyCompilerResult.CompiledAssembly, options);

                afterDetourTest(dynamicallyLoadedAssemblyCompilerResult);
            }
            catch (SourceCodeHasErrorsException e)
            {
                Debug.Log(e.Message);
            }
            catch (HotReloadCompilationException e)
            {
                Debug.Log($"Compilation Error: {e.InnerException}");
                InternalEditorUtility.OpenFileAtLineExternal(e.SourceCodeCombinedFileCreated, 0);
            }
            finally
            {
                try
                {
                    File.WriteAllText(filePath, originalSourceCode);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Unable to revert oto original code (test), make sure it's not locked in IDE. '{filePath}', {e}");
                    throw e;
                }
            }
        }

        protected static void AssertDetourConfirmed(Type type, string methodName, Func<object, bool> compareWithResultPredicate, string assertDescription)
        {
            Assert.IsTrue(TestDetourConfirmation.IsDetourConfirmed(type, methodName, compareWithResultPredicate), assertDescription);
        }

        private static CompileResult CompileCode(string filePath)
        {
            var dispatcher = new GameObject("DispatcherInstance").AddComponent<UnityMainThreadDispatcher>();
            return DynamicAssemblyCompiler.Compile(new List<string> { filePath }, dispatcher);
        }
        
        protected static string ResolveFullTestFilePath(string relativePath)
        {
            //TODO: how to resolve test path relative to proj dir for tests??
            return @$"E:\_src-unity\FastScriptReload\Assets\Tests\{relativePath}";
        }

        public void Setup()
        {
            TestDetourConfirmation.Clear();
        }
    }
}