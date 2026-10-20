using System;
using Newtonsoft.Json;

namespace MSBuildGraphHelper;

public static class HelperRunner
{
    public static object Run(CliOptions options)
    {
        MsBuildHost.RegisterFromMsBuildExe(options.MSBuildPath);

        return options.Mode switch
        {
            HelperRunMode.Closure => GraphExtractor.Extract(
                options.SolutionPath,
                options.ProjectName,
                options.Configuration,
                options.SolutionPlatform,
                options.VCTargetsPath),
            HelperRunMode.SourceFiles => string.IsNullOrWhiteSpace(options.ProjectName)
                ? SolutionSourceFileCatalog.ExtractSolution(
                    options.SolutionPath,
                    options.Configuration,
                    options.SolutionPlatform,
                    options.VCTargetsPath)
                : SolutionSourceFileCatalog.ExtractProject(
                    options.SolutionPath,
                    options.ProjectName,
                    options.Configuration,
                    options.SolutionPlatform,
                    options.VCTargetsPath),
            _ => throw new InvalidOperationException($"Unsupported mode: {options.Mode}")
        };
    }

    public static string RunToJson(CliOptions options)
    {
        var result = Run(options);
        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }
}
