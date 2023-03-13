using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using FastScriptReload.Runtime;
using ImmersiveVRTools.Editor.Common.Cache;
using ImmersiveVRTools.Runtime.Common;
using ImmersiveVrToolsCommon.Runtime.Logging;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Editor.Compilation
{
    [InitializeOnLoad]
    public class DotnetExeDynamicCompilation: DynamicCompilationBase
    {
        private static string _dotnetExePath;
        private static string _cscDll;
        private static string _tempFolder;

        private static string ApplicationContentsPath = EditorApplication.applicationContentsPath;
        private static readonly List<string> _createdFilesToCleanUp = new List<string>();

        static DotnetExeDynamicCompilation()
        {
#if UNITY_EDITOR_WIN
            const string dotnetExecutablePath = "dotnet.exe";
#else
            const string dotnetExecutablePath = "dotnet"; //mac and linux, no extension
#endif
                
            _dotnetExePath = FindFileOrThrow(dotnetExecutablePath);
            _cscDll = FindFileOrThrow("csc.dll"); //even on mac/linux need to find dll and use, not no extension one
            _tempFolder = Path.GetTempPath();
            
            EditorApplication.playModeStateChanged += obj =>
            {
                if (obj == PlayModeStateChange.ExitingPlayMode && _createdFilesToCleanUp.Any())
                {
                    LoggerScoped.LogDebug($"Removing temporary files: [{string.Join(",", _createdFilesToCleanUp)}]");
                    
                    foreach (var fileToCleanup in _createdFilesToCleanUp)
                    {
                        File.Delete(fileToCleanup);
                    }
                    _createdFilesToCleanUp.Clear();
                }
            };
        }

        private static string FindFileOrThrow(string fileName)
        {
            return SessionStateCache.GetOrCreateString($"FSR:FilePath_{fileName}", () =>
            {
                var foundFile = Directory
                    .GetFiles(ApplicationContentsPath, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (foundFile == null)
                {
                    throw new Exception($"Unable to find '{fileName}', make sure Editor version supports it. You can also add preprocessor directive 'FastScriptReload_CompileViaMCS' which will use Mono compiler instead");
                }

                return foundFile;
            });
        }

        public static CompileResult Compile(List<string> filePathsWithSourceCode, UnityMainThreadDispatcher unityMainThreadDispatcher)
        {
            try
            {
                var asmName = Guid.NewGuid().ToString().Replace("-", "");
                var rspFile = _tempFolder + $"{asmName}.rsp";
                var assemblyAttributeFilePath = _tempFolder + $"{asmName}.DynamicallyCreatedAssemblyAttribute.cs";
                var sourceCodeCombinedFilePath = _tempFolder + $"{asmName}.SourceCodeCombined.cs";
                var outLibraryPath = $"{_tempFolder}{asmName}.dll";

                var sourceCodeCombined = CreateSourceCodeCombinedContents(filePathsWithSourceCode, ActiveScriptCompilationDefines.ToList());
                CreateFileAndTrackAsCleanup(sourceCodeCombinedFilePath, sourceCodeCombined, _createdFilesToCleanUp);
#if UNITY_EDITOR
                unityMainThreadDispatcher.Enqueue(() =>
                {
                    if ((bool)FastScriptReloadPreference.IsAutoOpenGeneratedSourceFileOnChangeEnabled.GetEditorPersistedValueOrDefault())
                    {
                        UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(sourceCodeCombinedFilePath, 0);
                    }
                });
#endif

                var rspFileContent = GenerateCompilerArgsRspFileContents(outLibraryPath, _tempFolder, asmName, sourceCodeCombinedFilePath, assemblyAttributeFilePath);
                CreateFileAndTrackAsCleanup(rspFile, rspFileContent, _createdFilesToCleanUp);
                CreateFileAndTrackAsCleanup(assemblyAttributeFilePath, DynamicallyCreatedAssemblyAttributeSourceCode, _createdFilesToCleanUp);

                var exitCode = ExecuteDotnetExeCompilation(_dotnetExePath, _cscDll, rspFile, outLibraryPath, out var outputMessages);

                var compiledAssembly = Assembly.LoadFrom(outLibraryPath);
                return new CompileResult(outLibraryPath, outputMessages, exitCode, compiledAssembly, sourceCodeCombined, sourceCodeCombinedFilePath);
            }
            catch (Exception)
            {
                LoggerScoped.LogError($"Compilation error: temporary files were not removed so they can be inspected: " 
                               + string.Join(", ", _createdFilesToCleanUp
                                   .Select(f => $"<a href=\"{f}\" line=\"1\">{f}</a>")));
                if (LogHowToFixMessageOnCompilationError)
                {
                    LoggerScoped.LogWarning($@"HOW TO FIX - INSTRUCTIONS:

1) Open file that caused issue by looking at error log starting with: 'FSR: Compilation error: temporary files were not removed so they can be inspected: '. And click on file path to open.
2) Look up other error in the console, which will be like 'Error When updating files:' - this one contains exact line that failed to compile (in XXX_SourceCodeGenerated.cs file). Those are same compilation errors as you see in Unity/IDE when developing.
3) Read compiler error message as it'll help understand the issue

Error could be caused by a normal compilation issue that you created in source file (eg typo), in that case please fix and it'll recompile.

It's possible compilation fails due to existing limitation, in that case:

<b><color='orange'>You can quickly specify custom script rewrite override for part of code that's failing.</color></b>

Please use project panel to:
1) Right-click on the original file that has compilation issue
2) Click Fast Script Reload -> Add / Open User Script Rewrite Override
3) Read top comment in opened file and it'll explain how to create overrides

I'm continuously working on mitigating limitations.

If you could please get in touch with me via 'support@immersivevrtools.com' and include error you see in the console as well as created files (from paths in previous error). This way I can get it fixed for you.

You can also:
1) Look at 'limitation' section in the docs - which will explain bit more around limitations and workarounds
2) Move some of the code that you want to work on to different file - compilation happens on whole file, if you have multiple types there it could increase the chance of issues
3) Have a look at compilation error, it shows error line (in the '*.SourceCodeCombined.cs' file, it's going to be something that compiler does not accept, likely easy to spot. To workaround you can change that part of code in original file. It's specific patterns that'll break it.

*If you want to prevent that message from reappearing please go to Window -> Fast Script Reload -> Start Screen -> Logging -> tick off 'Log how to fix message on compilation error'*");

                }
                throw;
            }
        }

        private static void CreateFileAndTrackAsCleanup(string filePath, string contents, List<string> createdFilesToCleanUp)
        {
            File.WriteAllText(filePath, contents);
            createdFilesToCleanUp.Add(filePath);
        }

        private static string GenerateCompilerArgsRspFileContents(string outLibraryPath, string tempFolder, string asmName,
            string sourceCodeCombinedFilePath, string assemblyAttributeFilePath)
        {
            var rspContents = new StringBuilder();
            rspContents.AppendLine("-target:library");
            rspContents.AppendLine($"-out:\"{outLibraryPath}\"");
            rspContents.AppendLine($"-refout:\"{tempFolder}{asmName}.ref.dll\""); //TODO: what's that?
            foreach (var symbol in ActiveScriptCompilationDefines)
            {
                rspContents.AppendLine($"-define:{symbol}");
            }

            foreach (var referenceToAdd in ResolveReferencesToAdd(new List<string>()))
            {
                rspContents.AppendLine($"-r:\"{referenceToAdd}\"");
            }

            rspContents.AppendLine($"\"{sourceCodeCombinedFilePath}\"");
            rspContents.AppendLine($"\"{assemblyAttributeFilePath}\"");

            rspContents.AppendLine($"-langversion:latest");

            rspContents.AppendLine("/deterministic");
            rspContents.AppendLine("/optimize-");
            rspContents.AppendLine("/debug:portable");
            rspContents.AppendLine("/nologo");
            rspContents.AppendLine("/RuntimeMetadataVersion:v4.0.30319");

            rspContents.AppendLine("/nowarn:0169");
            rspContents.AppendLine("/nowarn:0649");
            rspContents.AppendLine("/nowarn:1701");
            rspContents.AppendLine("/nowarn:1702");
            rspContents.AppendLine("/utf8output");
            rspContents.AppendLine("/preferreduilang:en-US");
            
            var rspContentsString = rspContents.ToString();
            return rspContentsString;
        }

        private static int ExecuteDotnetExeCompilation(string dotnetExePath, string cscDll, string rspFile,
            string outLibraryPath, out List<string> outputMessages)
        {
            var process = new Process();
            process.StartInfo.FileName = dotnetExePath;
            process.StartInfo.Arguments = $"exec \"{cscDll}\" /nostdlib /noconfig /shared \"@{rspFile}\"";

            var outMessages = new List<string>();

            var stderr_completed = new ManualResetEvent(false);
            var stdout_completed = new ManualResetEvent(false);

            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                    outMessages.Add(args.Data);
                else
                    stderr_completed.Set();
            };
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    outMessages.Add(args.Data);
                    return;
                }

                stdout_completed.Set();
            };
            process.StartInfo.StandardOutputEncoding = process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                if (ex is Win32Exception win32Exception)
                    throw new SystemException(string.Format("Error running {0}: {1}", process.StartInfo.FileName,
                        typeof(Win32Exception)
                            .GetMethod("GetErrorMessage", BindingFlags.Static | BindingFlags.NonPublic)?
                            .Invoke(null, new object[] { win32Exception.NativeErrorCode }) ??
                        $"<Unable to resolve GetErrorMessage function>, NativeErrorCode: {win32Exception.NativeErrorCode}"));
                throw;
            }

            int exitCode = -1;
            try
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                exitCode = process.ExitCode;
            }
            finally
            {
                stderr_completed.WaitOne(TimeSpan.FromSeconds(30.0));
                stdout_completed.WaitOne(TimeSpan.FromSeconds(30.0));
                process.Close();
            }

            if (!File.Exists(outLibraryPath))
                throw new Exception("Compiler failed to produce the assembly. Output: '" +
                                    string.Join(Environment.NewLine + Environment.NewLine, outMessages) + "'");

            outputMessages = new List<string>();
            outputMessages.AddRange(outMessages);
            return exitCode;
        }
    }
}