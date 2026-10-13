using System;
using System.IO;
using Newtonsoft.Json;

namespace MSBuildGraphHelper;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            MsBuildHost.RegisterFromMsBuildExe(options.MSBuildPath);
            var graph = GraphExtractor.Extract(
                options.SolutionPath,
                options.ProjectName,
                options.Configuration,
                options.SolutionPlatform,
                options.VCTargetsPath);

            var json = JsonConvert.SerializeObject(graph, Formatting.Indented);
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                File.WriteAllText(options.OutputPath, json, System.Text.Encoding.UTF8);
            }
            else
            {
                Console.Out.Write(json);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine(ex.InnerException.Message);
            }

            return 1;
        }
    }

    private static Options ParseArgs(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--solution":
                    options.SolutionPath = RequireValue(args, ref i);
                    break;
                case "--project":
                    options.ProjectName = RequireValue(args, ref i);
                    break;
                case "--configuration":
                    options.Configuration = RequireValue(args, ref i);
                    break;
                case "--platform":
                    options.SolutionPlatform = RequireValue(args, ref i);
                    break;
                case "--output":
                    options.OutputPath = RequireValue(args, ref i);
                    break;
                case "--msbuild":
                    options.MSBuildPath = RequireValue(args, ref i);
                    break;
                case "--vctargets":
                    options.VCTargetsPath = RequireValue(args, ref i);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument: {args[i]}");
            }
        }

        if (string.IsNullOrWhiteSpace(options.SolutionPath))
        {
            throw new InvalidOperationException("--solution is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ProjectName))
        {
            throw new InvalidOperationException("--project is required.");
        }

        if (string.IsNullOrWhiteSpace(options.MSBuildPath))
        {
            throw new InvalidOperationException("--msbuild is required (path to Visual Studio MSBuild.exe).");
        }

        if (string.IsNullOrWhiteSpace(options.VCTargetsPath))
        {
            throw new InvalidOperationException("--vctargets is required (path to MSVC VCTargets folder, e.g. ...\\MSBuild\\Microsoft\\VC\\v170\\).");
        }

        return options;
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {args[index]}.");
        }

        index++;
        return args[index];
    }

    private sealed class Options
    {
        public string SolutionPath { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Configuration { get; set; } = "Debug";
        public string SolutionPlatform { get; set; } = "x64";
        public string MSBuildPath { get; set; } = "";
        public string VCTargetsPath { get; set; } = "";
        public string? OutputPath { get; set; }
    }
}
