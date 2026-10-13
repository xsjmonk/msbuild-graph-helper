using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Locator;

namespace MSBuildGraphHelper;

internal static class MsBuildHost
{
    private static bool _registered;

    public static void RegisterFromMsBuildExe(string msbuildExePath)
    {
        if (_registered)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(msbuildExePath))
        {
            throw new InvalidOperationException("MSBuild.exe path is required for SDK-style project evaluation.");
        }

        msbuildExePath = Path.GetFullPath(msbuildExePath);
        if (!File.Exists(msbuildExePath))
        {
            throw new InvalidOperationException($"MSBuild.exe not found: {msbuildExePath}");
        }

        var msbuildBinDirectory = Path.GetDirectoryName(msbuildExePath)
            ?? throw new InvalidOperationException($"Invalid MSBuild path: {msbuildExePath}");

        var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
        if (instances.Count == 0)
        {
            throw new InvalidOperationException("MSBuildLocator found no Visual Studio MSBuild instances.");
        }

        var match = instances
            .FirstOrDefault(i => PathsEqual(i.MSBuildPath, msbuildBinDirectory))
            ?? instances.OrderByDescending(i => i.Version).First();

        MSBuildLocator.RegisterInstance(match);
        _registered = true;
    }

    private static bool PathsEqual(string a, string b)
    {
        return string.Equals(
            Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
    }
}
