using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MSBuildGraphHelper;
using Xunit;

namespace MSBuildGraphHelper.Tests;

public sealed class MsBuildTestEnvironment
{
    public string MsBuildPath { get; }
    public string? VcTargetsPath { get; }
    public bool VcToolsAvailable => VcTargetsPath != null;

    public MsBuildTestEnvironment()
    {
        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");
        if (!File.Exists(vswhere))
        {
            throw new InvalidOperationException(
                "vswhere.exe not found; Visual Studio with MSBuild is a hard prerequisite for these tests.");
        }

        var installPath = RunProcess(vswhere, "-latest -requires Microsoft.Component.MSBuild -property installationPath").Trim();
        if (string.IsNullOrWhiteSpace(installPath))
        {
            throw new InvalidOperationException(
                "vswhere.exe not found; Visual Studio with MSBuild is a hard prerequisite for these tests.");
        }

        MsBuildPath = Path.Combine(installPath, "MSBuild", "Current", "Bin", "amd64", "MSBuild.exe");
        if (!File.Exists(MsBuildPath))
        {
            throw new InvalidOperationException($"MSBuild.exe not found: {MsBuildPath}");
        }

        VcTargetsPath = FindVcTargetsPath(installPath);
        MsBuildHost.RegisterFromMsBuildExe(MsBuildPath);
    }

    private static string? FindVcTargetsPath(string installationPath)
    {
        var vcRoot = Path.Combine(installationPath, "MSBuild", "Microsoft", "VC");
        if (!Directory.Exists(vcRoot))
        {
            return null;
        }

        var candidates = Directory.GetDirectories(vcRoot)
            .Where(d => File.Exists(Path.Combine(d, "Microsoft.Cpp.Default.props")))
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Cast<string>()
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferred = candidates.FirstOrDefault(n => string.Equals(n, "v170", StringComparison.OrdinalIgnoreCase));
        var chosen = preferred ?? candidates.FirstOrDefault();
        if (chosen == null)
        {
            return null;
        }

        return Path.Combine(vcRoot, chosen);
    }

    private static string RunProcess(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {fileName}");
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return stdout;
    }
}

[CollectionDefinition("MsBuildCollection")]
public sealed class MsBuildCollection : ICollectionFixture<MsBuildTestEnvironment>
{
}
