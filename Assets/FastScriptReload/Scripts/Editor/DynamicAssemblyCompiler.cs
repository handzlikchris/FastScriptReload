using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using QuickCodeIteration.Scripts.Editor;
using Debug = UnityEngine.Debug;

public class DynamicAssemblyCompiler
{
    public static CompileResult Compile(List<string> filePathsWithSourceCode)
    {
        var sw = new Stopwatch();
        sw.Start();
        
#if QuickCodeIterationManager_CompileViaMCS
        var result = McsExeDynamicCompilation.Compile(filePathsWithSourceCode);
#else
        var result = DotnetExeDynamicCompilation.Compile(filePathsWithSourceCode);
#endif  
        
        Debug.Log($"Files: {string.Join(",", filePathsWithSourceCode.Select(fn => new FileInfo(fn).Name))} changed - compilation (took {sw.ElapsedMilliseconds}ms)");
        return result;
    }
}

public class CompileResult
{
    public Assembly CompiledAssembly { get; }
    public string CompiledAssemblyPath { get; }
    public List<string> MessagesFromCompilerProcess { get; }
    public bool IsError => string.IsNullOrEmpty(CompiledAssemblyPath);
    public int NativeCompilerReturnValue { get; }
    public string SourceCodeCombined { get; }

    public CompileResult(string compiledAssemblyPath, List<string> messagesFromCompilerProcess, int nativeCompilerReturnValue, Assembly compiledAssembly, string sourceCodeCombined)
    {
        CompiledAssemblyPath = compiledAssemblyPath;
        MessagesFromCompilerProcess = messagesFromCompilerProcess;
        NativeCompilerReturnValue = nativeCompilerReturnValue;
        CompiledAssembly = compiledAssembly;
        SourceCodeCombined = sourceCodeCombined;
    }
}