using System;
using System.IO;

namespace MSBuildGraphHelper;

internal static class MsBuildVcTargets
{
    public static string Normalize(string vcTargetsPath, string requiredForMessage)
    {
        if (string.IsNullOrWhiteSpace(vcTargetsPath))
        {
            throw new InvalidOperationException(requiredForMessage);
        }

        var normalized = Path.GetFullPath(vcTargetsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        normalized += Path.DirectorySeparatorChar;

        var defaultProps = Path.Combine(normalized, "Microsoft.Cpp.Default.props");
        if (!File.Exists(defaultProps))
        {
            throw new InvalidOperationException($"VCTargetsPath is invalid (missing Microsoft.Cpp.Default.props): {normalized}");
        }

        return normalized;
    }
}
