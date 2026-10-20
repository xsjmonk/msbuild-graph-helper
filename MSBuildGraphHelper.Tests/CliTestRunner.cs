using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MSBuildGraphHelper.Tests;

internal static class CliTestRunner
{
    public static (int ExitCode, string StdOut, string StdErr) Run(IEnumerable<string> arguments)
    {
        var dllPath = Path.Combine(AppContext.BaseDirectory, "MSBuildGraphHelper.dll");
        if (!File.Exists(dllPath))
        {
            throw new FileNotFoundException("MSBuildGraphHelper.dll not found in test output.", dllPath);
        }

        var args = new List<string> { dllPath };
        args.AddRange(arguments);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet exec.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, stdout, stderr);
    }
}
