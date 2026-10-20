using System;
using System.Collections.Generic;
using System.IO;

namespace MSBuildGraphHelper;

internal static class MsBuildSolutionDir
{
    /// <summary>
    /// Directory containing the .sln, with trailing directory separator (MSBuild / Visual Studio convention).
    /// </summary>
    public static string FromSolutionPath(string solutionPath)
    {
        if (string.IsNullOrWhiteSpace(solutionPath))
        {
            throw new InvalidOperationException("Solution path is required to compute SolutionDir.");
        }

        var fullPath = Path.GetFullPath(solutionPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"Cannot resolve solution directory from path: {solutionPath}");
        }

        if (!directory.EndsWith(Path.DirectorySeparatorChar) && !directory.EndsWith(Path.AltDirectorySeparatorChar))
        {
            directory += Path.DirectorySeparatorChar;
        }

        return directory;
    }

    public static void ApplyToGlobalProperties(IDictionary<string, string> globalProperties, string solutionPath)
    {
        globalProperties["SolutionDir"] = FromSolutionPath(solutionPath);
    }
}
