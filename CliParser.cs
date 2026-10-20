using System;

namespace MSBuildGraphHelper;

public static class CliParser
{
    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                    options.Mode = ParseMode(RequireValue(args, ref i));
                    break;
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

        if (string.IsNullOrWhiteSpace(options.MSBuildPath))
        {
            throw new InvalidOperationException("--msbuild is required (path to Visual Studio MSBuild.exe).");
        }

        if (string.IsNullOrWhiteSpace(options.VCTargetsPath))
        {
            throw new InvalidOperationException("--vctargets is required (path to MSVC VCTargets folder, e.g. ...\\MSBuild\\Microsoft\\VC\\v170\\).");
        }

        if (options.Mode == HelperRunMode.Closure && string.IsNullOrWhiteSpace(options.ProjectName))
        {
            throw new InvalidOperationException("--project is required for --mode closure.");
        }

        return options;
    }

    private static HelperRunMode ParseMode(string value)
    {
        return value switch
        {
            "closure" => HelperRunMode.Closure,
            "source-files" => HelperRunMode.SourceFiles,
            _ => throw new InvalidOperationException($"Unknown --mode '{value}'. Use 'closure' or 'source-files'.")
        };
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
}
