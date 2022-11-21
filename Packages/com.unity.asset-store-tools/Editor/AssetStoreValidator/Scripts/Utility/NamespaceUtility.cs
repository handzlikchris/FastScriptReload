using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityObject = UnityEngine.Object;

namespace AssetStoreTools.Validator
{
    internal static class NamespaceUtility
    {
        private const int ScriptTimeoutMs = 10000;
        private const string IgnoredAssemblyCharacters = "<>=";

        public static IReadOnlyDictionary<UnityObject, IList<string>> GetTypesWithoutNamespacesFromAssemblies(IList<UnityObject> assemblies)
        {
            var dllPaths = assemblies.ToDictionary(t => AssetDatabase.GetAssetPath(t), t => t);
            var affectedDllPaths = new ConcurrentDictionary<string, IList<string>>();
            var failedDllPaths = new ConcurrentBag<string>();

            Parallel.ForEach(dllPaths.Keys,
                (dll) =>
                {
                    if (!GetTypesWithoutNamespacesFromAssembly(dll, out IList<string> affectedTypes))
                        failedDllPaths.Add(dll);
                    else if (affectedTypes.Count > 0)
                        affectedDllPaths.TryAdd(dll, affectedTypes);
                });

            if (failedDllPaths.Count > 0)
            {
                var message = new StringBuilder("The following precompiled assemblies could not be checked for types without namespaces:");
                foreach (var path in failedDllPaths)
                    message.Append($"\n{path}");
                UnityEngine.Debug.LogWarning(message);
            }

            var affectedDlls = new Dictionary<UnityObject, IList<string>>();

            foreach (var kvp in affectedDllPaths)
                affectedDlls.Add(dllPaths[kvp.Key], kvp.Value);

            return affectedDlls;
        }

        private static bool GetTypesWithoutNamespacesFromAssembly(string assemblyPath, out IList<string> affectedTypes)
        {
            affectedTypes = new List<string>();

            try
            {
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault
                (x => Path.GetFullPath(x.Location).Equals(Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase));
                if (assembly == null)
                    assembly = Assembly.LoadFrom(assemblyPath);

                var types = assembly.GetTypes();

                foreach (var t in types)
                {
                    if (t.Namespace == null && !t.Name.Any(x => IgnoredAssemblyCharacters.Contains(x)))
                    {
                        var name = t.Name;
                        if (t.IsClass)
                            name = "class " + name;
                        else if (t.IsInterface)
                            name = "interface " + name;
                        else if (t.IsEnum)
                            name = "enum " + name;
                        else if (t.IsValueType)
                            name = "struct " + name;
                        affectedTypes.Add(name);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyDictionary<MonoScript, IList<string>> GetTypesWithoutNamespacesFromScripts(IList<MonoScript> monoScripts)
        {
            var monoScriptContents = new Dictionary<MonoScript, string>();
            var affectedScripts = new ConcurrentDictionary<MonoScript, IList<string>>();
            var failedScripts = new ConcurrentBag<MonoScript>();

            // A separate dictionary is needed because MonoScript contents cannot be accessed outside of the main thread
            foreach (var kvp in monoScripts)
                monoScriptContents.Add(kvp, kvp.text);

            var tasks = new List<Tuple<Task, CancellationTokenSource>>();
            try
            {
                foreach (var kvp in monoScriptContents)
                {
                    var cancellationTokenSource = new CancellationTokenSource(ScriptTimeoutMs);
                    var task = Task.Run(() =>
                    {
                        var parsingTask = new ScriptParser(cancellationTokenSource.Token);
                        var parsed = parsingTask.Run(kvp.Value, out List<string> types);
                        if (!parsed)
                            failedScripts.Add(kvp.Key);
                        else if (types.Count > 0)
                            affectedScripts.TryAdd(kvp.Key, types);
                    });

                    tasks.Add(new Tuple<Task, CancellationTokenSource>(task, cancellationTokenSource));
                }

                foreach (var t in tasks)
                    t.Item1.Wait();
            }
            finally
            {
                foreach (var t in tasks)
                    t.Item2.Dispose();
            }

            if (failedScripts.Count > 0)
            {
                var message = new StringBuilder("The following scripts could not be checked for types without namespaces:");
                foreach (var s in failedScripts)
                    message.Append($"\n{AssetDatabase.GetAssetPath(s)}");
                UnityEngine.Debug.LogWarning(message);
            }

            // Instead of returning 'affectedScripts' which is sorted randomly every time,
            // return scripts in the same order as they were retrieved from the ADB
            var affectedScriptsSorted = monoScriptContents.Where(x => affectedScripts.ContainsKey(x.Key))
                .Select(x => new KeyValuePair<MonoScript, IList<string>>(x.Key, affectedScripts[x.Key]))
                .ToDictionary(t => t.Key, t => t.Value);

            return affectedScriptsSorted;
        }

        private class ScriptParser
        {
            private class BlockInfo
            {
                public string TypeName;
                public string Name;
                public int FoundIndex;
                public int StartIndex;
            }

            private CancellationToken _token;

            public ScriptParser(CancellationToken token)
            {
                _token = token;
            }

            public bool Run(string text, out List<string> typesWithoutNamespace)
            {
                typesWithoutNamespace = new List<string>();

                try
                {
                    var sanitized = SanitizeScript(text);
                    typesWithoutNamespace = ScanForTypesWithoutNamespaces(sanitized);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private string SanitizeScript(string source)
            {
                var sb = new StringBuilder(source);

                // Remove comments and strings
                sb = RemoveStringsAndComments(sb);

                // Replace newlines with spaces
                sb.Replace("\r", " ").Replace("\n", " ");

                // Space out the brackets
                sb.Replace("{", " { ").Replace("}", " } ");

                // Insert a space at the start for more convenient parsing
                sb.Insert(0, " ");

                // Remove repeating spaces
                var sanitized = Regex.Replace(sb.ToString(), @"\s{2,}", " ");

                return sanitized;
            }

            private StringBuilder RemoveStringsAndComments(StringBuilder sb)
            {
                void CheckStringIdentifiers(int index, out bool isVerbatim, out bool isInterpolated)
                {
                    isVerbatim = false;
                    isInterpolated = false;

                    string precedingChars = string.Empty;
                    for (int i = index - 1; i >= 0; i--)
                    {
                        if (sb[i] == ' ')
                            break;
                        precedingChars += sb[i];
                    }

                    if (precedingChars.Contains("@"))
                        isVerbatim = true;
                    if (precedingChars.Contains("$"))
                        isInterpolated = true;
                }

                bool IsRegion(int index)
                {
                    if (sb.Length - index < "#region".Length)
                        return false;
                    if (sb[index] == '#' && sb[index + 1] == 'r' && sb[index + 2] == 'e' && sb[index + 3] == 'g' && sb[index + 4] == 'i' &&
                        sb[index + 5] == 'o' && sb[index + 6] == 'n')
                        return true;
                    return false;
                }

                var removeRanges = new List<Tuple<int, int>>();

                for (int i = 0; i < sb.Length; i++)
                {
                    _token.ThrowIfCancellationRequested();

                    // Comment code
                    if (sb[i] == '/')
                    {
                        if (sb[i + 1] == '/')
                        {
                            for (int j = i + 1; j < sb.Length; j++)
                            {
                                _token.ThrowIfCancellationRequested();
                                if (sb[j] == '\n' || j == sb.Length - 1)
                                {
                                    removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                    i = j;
                                    break;
                                }
                            }
                        }
                        else if (sb[i + 1] == '*')
                        {
                            for (int j = i + 2; j < sb.Length; j++)
                            {
                                _token.ThrowIfCancellationRequested();
                                if (sb[j] == '/' && sb[j - 1] == '*')
                                {
                                    removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                    i = j + 1;
                                    break;
                                }
                            }
                        }
                    }
                    // Char code
                    else if(sb[i] == '\'')
                    {
                        for(int j = i + 1; j < sb.Length; j++)
                        {
                            _token.ThrowIfCancellationRequested();
                            if(sb[j] == '\'')
                            {
                                if(sb[j - 1] == '\\')
                                {
                                    int slashCount = 0;
                                    int k = j - 1;
                                    while (sb[k--] == '\\')
                                        slashCount++;
                                    if (slashCount % 2 != 0)
                                        continue;
                                }    
                                removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                i = j;
                                break;
                            }
                        }
                    }
                    // String code
                    else if (sb[i] == '"')
                    {
                        if (sb[i - 1] == '\'' && sb[i + 1] == '\'' || (sb[i - 2] == '\'' && sb[i - 1] == '\\' && sb[i + 1] == '\''))
                            continue;

                        CheckStringIdentifiers(i, out bool isVerbatim, out bool isInterpolated);

                        var bracketCount = 0;
                        bool interpolationEnd = true;
                        for (int j = i + 1; j < sb.Length; j++)
                        {
                            _token.ThrowIfCancellationRequested();
                            if (isInterpolated && (sb[j] == '{' || sb[j] == '}'))
                            {
                                if (sb[j] == '{')
                                {
                                    if (sb[j + 1] != '{')
                                        bracketCount++;
                                    else
                                        j += 1;
                                }
                                else if (sb[j] == '}')
                                {
                                    if (sb[j + 1] != '}')
                                        bracketCount--;
                                    else
                                        j += 1;
                                }

                                if (bracketCount == 0)
                                    interpolationEnd = true;
                                else
                                    interpolationEnd = false;

                                continue;
                            }

                            if (sb[j] == '\"')
                            {
                                if (isVerbatim)
                                {
                                    if (sb[j + 1] != '\"')
                                    {
                                        if (!isInterpolated || isInterpolated && interpolationEnd == true)
                                        {
                                            removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                            i = j + 1;
                                            break;
                                        }
                                    }
                                    else
                                        j += 1;
                                }
                                else
                                {
                                    bool endOfComment = false;
                                    if (sb[j - 1] != '\\')
                                        endOfComment = true;
                                    else
                                    {
                                        int slashCount = 0;
                                        int k = j - 1;
                                        while (sb[k--] == '\\')
                                            slashCount++;
                                        if (slashCount % 2 == 0)
                                            endOfComment = true;
                                    }

                                    if (!isInterpolated && endOfComment || (isInterpolated && interpolationEnd && endOfComment))
                                    {
                                        removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                        i = j + 1;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    // Region code
                    else if (IsRegion(i))
                    {
                        i += "#region".Length;
                        for(int j = i; j < sb.Length; j++)
                        {
                            _token.ThrowIfCancellationRequested();
                            if(sb[j] == '\n')
                            {
                                removeRanges.Add(new Tuple<int, int>(i, j - i + 1));
                                i = j;
                                break;
                            }
                        }
                    }
                }

                for (int i = removeRanges.Count - 1; i >= 0; i--)
                    sb = sb.Remove(removeRanges[i].Item1, removeRanges[i].Item2);

                return sb;
            }

            private List<string> ScanForTypesWithoutNamespaces(string script)
            {
                var typesWithoutNamespaces = new List<string>();
                var blockStack = new Stack<string>();
                int i = 0;

                BlockInfo nextNamespace = null;
                BlockInfo nextClass = null;
                BlockInfo nextStruct = null;
                BlockInfo nextInterface = null;
                BlockInfo nextEnum = null;

                while (i < script.Length)
                {
                    _token.ThrowIfCancellationRequested();
                    if (nextNamespace == null)
                        nextNamespace = FindNextTypeBlock(script, i, "namespace");
                    if (nextClass == null)
                        nextClass = FindNextTypeBlock(script, i, "class");
                    if (nextStruct == null)
                        nextStruct = FindNextTypeBlock(script, i, "struct");
                    if (nextInterface == null)
                        nextInterface = FindNextTypeBlock(script, i, "interface");
                    if (nextEnum == null)
                        nextEnum = FindNextTypeBlock(script, i, "enum");

                    var nextIdentationIncrease = FindNextIdentationIncrease(script, i);
                    var nextIdentationDecrease = FindNextIdentationDecrease(script, i);

                    if (!TryFindClosestBlock(out var closestBlock, nextNamespace, nextClass,
                        nextStruct, nextInterface, nextEnum, nextIdentationIncrease, nextIdentationDecrease))
                        break;

                    switch (closestBlock)
                    {
                        case var _ when closestBlock == nextNamespace:
                            blockStack.Push("namespace");
                            nextNamespace = null;
                            break;
                        case var _ when closestBlock == nextIdentationIncrease:
                            blockStack.Push("{");
                            break;
                        case var _ when closestBlock == nextIdentationDecrease:
                            blockStack.Pop();
                            break;
                        case var _ when closestBlock == nextClass:
                        case var _ when closestBlock == nextStruct:
                        case var _ when closestBlock == nextInterface:
                        case var _ when closestBlock == nextEnum:
                            if (!blockStack.Contains("namespace"))
                                typesWithoutNamespaces.Add(closestBlock.TypeName + " " + closestBlock.Name);
                            blockStack.Push(closestBlock.TypeName);
                            switch (closestBlock)
                            {
                                case var _ when closestBlock == nextClass:
                                    nextClass = null;
                                    break;
                                case var _ when closestBlock == nextStruct:
                                    nextStruct = null;
                                    break;
                                case var _ when closestBlock == nextInterface:
                                    nextInterface = null;
                                    break;
                                case var _ when closestBlock == nextEnum:
                                    nextEnum = null;
                                    break;
                            }
                            break;
                    }

                    i = closestBlock.StartIndex;
                }

                return typesWithoutNamespaces;
            }

            private bool TryFindClosestBlock(out BlockInfo closestBlock, params BlockInfo[] blocks)
            {
                closestBlock = null;
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (blocks[i].FoundIndex == -1)
                        continue;

                    if (closestBlock == null || closestBlock.FoundIndex > blocks[i].FoundIndex)
                        closestBlock = blocks[i];
                }

                return closestBlock != null;
            }

            private BlockInfo FindNextTypeBlock(string text, int startIndex, string typeKeyword)
            {
                int start = -1;
                int blockStart = -1;
                string name = string.Empty;
                while (startIndex < text.Length)
                {
                    _token.ThrowIfCancellationRequested();
                    start = text.IndexOf($" {typeKeyword} ", startIndex);
                    if (start == -1)
                        return new BlockInfo { FoundIndex = -1 };

                    // Check if the caught type keyword matches the type definition
                    var openingBracket = text.IndexOf("{", start);
                    if (openingBracket == -1)
                        return new BlockInfo { FoundIndex = -1 };

                    var declaration = text.Substring(start, openingBracket - start);
                    var split = declaration.Split(' ');

                    // Namespace detection
                    if (typeKeyword == "namespace")
                    {
                        // Expected result: [null] [namespace] [null]
                        if (split.Length == 4)
                        {
                            name = split[2];
                            blockStart = openingBracket + 1;
                            break;
                        }
                        else
                            startIndex = openingBracket + 1;
                    }
                    // Class, Interface, Struct, Enum detection
                    else
                    {
                        // Expected result: [null] [keywordName] [typeName] ... [null]
                        // Skip any keywords that only contains [null] [keywordName] [null]
                        if (split.Length != 3)
                        {
                            name = split[2];
                            blockStart = openingBracket + 1;
                            break;
                        }
                        else
                            startIndex = openingBracket + 1;
                    }
                }

                return new BlockInfo() { FoundIndex = start, StartIndex = blockStart, Name = name, TypeName = typeKeyword };
            }

            private BlockInfo FindNextIdentationIncrease(string text, int startIndex)
            {
                var foundAt = text.IndexOf("{", startIndex);
                return new BlockInfo() { FoundIndex = foundAt, StartIndex = foundAt + 1, Name = "{" };
            }

            private BlockInfo FindNextIdentationDecrease(string text, int startIndex)
            {
                var foundAt = text.IndexOf("}", startIndex);
                return new BlockInfo() { FoundIndex = foundAt, StartIndex = foundAt + 1, Name = "}" };
            }
        }
    }
}