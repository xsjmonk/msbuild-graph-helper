using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using MSBuildGraphHelper;
using Newtonsoft.Json;
using Xunit;

namespace MSBuildGraphHelper.Tests;

[Collection("MsBuildCollection")]
public sealed class SourceFilesModeTests
{
    private readonly MsBuildTestEnvironment _env;

    public SourceFilesModeTests(MsBuildTestEnvironment env) => _env = env;

    [Fact]
    public void Cli_SourceFilesMode_WholeSolution_ListsAllBuildableProjects()
    {
        var (exitCode, stdout, _) = RunSourceFiles([]);
        exitCode.Should().Be(0);

        var catalog = JsonConvert.DeserializeObject<SolutionSourceFilesDto>(stdout);
        catalog.Should().NotBeNull();
        catalog!.SelectedProjectName.Should().BeNull();
        catalog.Projects.Select(p => p.ProjectName).Should().BeEquivalentTo(
            ["ProjectA", "ProjectB", "ProjectC", "ProjectD", "ProjectX"]);
        catalog.Projects.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.ProjectPath));
        catalog.Projects.Should().OnlyContain(p => p.SourceFilePaths.Count > 0);
    }

    [Fact]
    public void Cli_SourceFilesMode_SingleProject_ListsOneEntry()
    {
        var (exitCode, stdout, _) = RunSourceFiles(["--project", "ProjectA"]);
        exitCode.Should().Be(0);

        var catalog = JsonConvert.DeserializeObject<SolutionSourceFilesDto>(stdout);
        catalog.Should().NotBeNull();
        catalog!.SelectedProjectName.Should().Be("ProjectA");
        catalog.Projects.Should().ContainSingle();
        catalog.Projects[0].ProjectName.Should().Be("ProjectA");
        catalog.Projects[0].ProjectPath.Should().Contain("ProjectA" + Path.DirectorySeparatorChar + "ProjectA.csproj");
    }

    [Fact]
    public void Cli_SourceFilesMode_ProjectA_ContainsA1_NotExcludedSkip()
    {
        var (exitCode, stdout, _) = RunSourceFiles(["--project", "ProjectA"]);
        exitCode.Should().Be(0);

        var catalog = JsonConvert.DeserializeObject<SolutionSourceFilesDto>(stdout)!;
        var paths = catalog.Projects[0].SourceFilePaths;
        paths.Should().Contain(p => p.EndsWith("ProjectA\\A1.cs", StringComparison.OrdinalIgnoreCase));
        paths.Should().NotContain(p => p.EndsWith("Excluded\\Skip.cs", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Cli_SourceFilesMode_UnknownProject_Fails()
    {
        var (exitCode, _, stderr) = RunSourceFiles(["--project", "NoSuchProject"]);
        exitCode.Should().Be(1);
        stderr.Should().Contain("No solution project named 'NoSuchProject'");
    }

    [Fact]
    public void Cli_ClosureMode_StillRequiresProject()
    {
        var args = BaseArgs().Concat(["--mode", "closure", "--project", "ProjectD"]).ToList();
        var projectFlagIndex = args.IndexOf("--project");
        args.RemoveAt(projectFlagIndex + 1);
        args.RemoveAt(projectFlagIndex);

        var (exitCode, _, stderr) = CliTestRunner.Run(args);
        exitCode.Should().Be(1);
        stderr.Should().Contain("--project is required for --mode closure");
    }

    [Fact]
    public void Catalog_ExtractProject_MatchesCli()
    {
        MsBuildHost.RegisterFromMsBuildExe(_env.MsBuildPath);
        var catalog = SolutionSourceFileCatalog.ExtractProject(
            FixturePaths.SampleSolutionPath,
            "ProjectD",
            "Debug",
            "x64",
            _env.VcTargetsPath ?? throw new InvalidOperationException("VC targets required."));

        catalog.Projects.Should().ContainSingle();
        catalog.Projects[0].ProjectName.Should().Be("ProjectD");
        catalog.Projects[0].SourceFilePaths.Should().Contain(p =>
            p.EndsWith("ProjectD\\D1.cs", StringComparison.OrdinalIgnoreCase));
    }

    private (int, string, string) RunSourceFiles(string[] extraArgs)
    {
        var args = BaseArgs().Concat(extraArgs).ToArray();
        if (!args.Contains("--mode"))
        {
            args = args.Concat(["--mode", "source-files"]).ToArray();
        }

        return CliTestRunner.Run(args);
    }

    private string[] BaseArgs() =>
    [
        "--solution", FixturePaths.SampleSolutionPath,
        "--configuration", "Debug",
        "--platform", "x64",
        "--msbuild", _env.MsBuildPath,
        "--vctargets", _env.VcTargetsPath ?? throw new InvalidOperationException("VC targets required for source-files tests.")
    ];
}
