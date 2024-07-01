using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

public static class PartialClassFinder
{
    public static IEnumerable<string> FindPartialClassFilesInDirectory(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return Enumerable.Empty<string>();
        }

        string directory = Path.GetDirectoryName(filePath);
        string fileContent = File.ReadAllText(filePath);

        string partialClassName = ExtractPartialClassName(fileContent);
        if (string.IsNullOrEmpty(partialClassName))
        {
            Debug.LogWarning($"No partial class/struct found in file: {filePath}");
            return Enumerable.Empty<string>();
        }

        return Directory.GetFiles(directory, "*.cs")
                .Where(file => IsPartialClassFile(file, partialClassName));
    }

    /**
     * Well, se queja de
     */
    private static string ExtractPartialClassName(string fileContent)
    {
        string pattern = @"partial\s+(class|struct)\s+(\w+)";
        Match match = Regex.Match(fileContent, pattern);
        return match.Success ? match.Groups[2].Value : string.Empty;
    }

    private static bool IsPartialClassFile(string filePath, string partialClassName)
    {
        string content = File.ReadAllText(filePath);
        string pattern = $@"partial\s+(class|struct)\s+{Regex.Escape(partialClassName)}";
        return Regex.IsMatch(content, pattern);
    }
}
