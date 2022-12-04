using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace FastScriptReload.Editor
{
    [InitializeOnLoad]
    public class DotnetExeDynamicCompilation: DynamicCompilationBase
    {
        private static string _dotnetExePath;
        private static string _cscDll;
        private static string _tempFolder;

        static DotnetExeDynamicCompilation()
        {
            //TODO: save in player prefs so it stays between project opens
            _dotnetExePath = FindFileOrThrow("dotnet.exe");
            _cscDll = FindFileOrThrow("csc.dll");
            _tempFolder = Path.GetTempPath();
        }

        private static string FindFileOrThrow(string fileName)
        {
            var foundFile = Directory
                .GetFiles(EditorApplication.applicationContentsPath, fileName, SearchOption.AllDirectories)
                .FirstOrDefault();
            if (foundFile == null)
            {
                throw new Exception($"Unable to find '{fileName}', make sure Editor version supports it. You can also add preprocessor directive 'QuickCodeIterationManager_CompileViaMCS' which will use Mono compiler instead");
            }

            return foundFile;
        }

        public static CompileResult Compile(List<string> filePathsWithSourceCode)
        {
            var createdFilesToCleanUp = new List<string>();
            try
            {
                var asmName = Guid.NewGuid().ToString().Replace("-", "");
                var rspFile = _tempFolder + $"{asmName}.rsp";
                var assemblyAttributeFilePath = _tempFolder + $"{asmName}.DynamicallyCreatedAssemblyAttribute.cs";
                var sourceCodeCombinedFilePath = _tempFolder + $"{asmName}.SourceCodeCombined.cs";
                var outLibraryPath = $"{_tempFolder}{asmName}.dll";

                var sourceCodeCombined = CreateSourceCodeCombinedContents(filePathsWithSourceCode.Select(File.ReadAllText));
                CreateFileAndTrackAsCleanup(sourceCodeCombinedFilePath, sourceCodeCombined, createdFilesToCleanUp);

                var rspFileContent = GenerateCompilerArgsRspFileContents(outLibraryPath, _tempFolder, asmName, sourceCodeCombinedFilePath, assemblyAttributeFilePath);
                CreateFileAndTrackAsCleanup(rspFile, rspFileContent, createdFilesToCleanUp);
                CreateFileAndTrackAsCleanup(assemblyAttributeFilePath, DynamicallyCreatedAssemblyAttributeSourceCode, createdFilesToCleanUp);

                var exitCode = ExecuteDotnetExeCompilation(_dotnetExePath, _cscDll, rspFile, outLibraryPath, out var outputMessages);

                var compiledAssembly = Assembly.LoadFrom(outLibraryPath);
                
                foreach (var fileToCleanup in createdFilesToCleanUp)
                {
                    File.Delete(fileToCleanup);
                }
                
                return new CompileResult(outLibraryPath, outputMessages, exitCode, compiledAssembly, sourceCodeCombined);
            }
            catch (Exception)
            {
                Debug.LogError($"Compilation error: temporary files were not removed so they can be inspected: {string.Join(", ", createdFilesToCleanUp)}");
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

            // rspContents.AppendLine("/additionalfile:\"Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.AdditionalFile.txt\""); //TODO: needed?
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