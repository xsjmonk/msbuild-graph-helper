using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using MSBuildGraphHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MSBuildGraphHelper.Tests;

[Collection("MsBuildCollection")]
public sealed class BackwardCompatibilityRegressionTests
{
    private readonly MsBuildTestEnvironment _env;

    public BackwardCompatibilityRegressionTests(MsBuildTestEnvironment env) => _env = env;

    [Fact]
    public void Cli_DefaultMode_SampleSolutionProjectD_ClosureStructureUnchanged()
    {
        var (exitCode, stdout, _) = RunClosure([]);
        exitCode.Should().Be(0);
        var graph = JsonConvert.DeserializeObject<SolutionGraphDto>(stdout);
        graph.Should().NotBeNull();
        AssertClosureStructure(graph!);
    }

    [Fact]
    public void Cli_DefaultMode_MissingProject_StillFailsAsToday()
    {
        var args = BaseArgs().ToList();
        var projectFlagIndex = args.IndexOf("--project");
        args.RemoveAt(projectFlagIndex + 1);
        args.RemoveAt(projectFlagIndex);
        var (exitCode, _, stderr) = CliTestRunner.Run(args);
        exitCode.Should().Be(1);
        stderr.Should().Contain("--project is required");
    }

    [Fact]
    public void Cli_DefaultMode_JsonRootIsSolutionGraphDto_NotAffectedPlan()
    {
        var (exitCode, stdout, _) = RunClosure([]);
        exitCode.Should().Be(0);
        var root = JObject.Parse(stdout);
        root.ContainsKey("BuildOrder").Should().BeTrue();
        root.ContainsKey("AffectedBuildOrder").Should().BeFalse();
    }

    private (int, string, string) RunClosure(string[] extraArgs)
    {
        var args = BaseArgs().Concat(extraArgs).ToArray();
        return CliTestRunner.Run(args);
    }

    private string[] BaseArgs() =>
    [
        "--solution", FixturePaths.SampleSolutionPath,
        "--project", "ProjectD",
        "--configuration", "Debug",
        "--platform", "x64",
        "--msbuild", _env.MsBuildPath,
        "--vctargets", _env.VcTargetsPath ?? throw new InvalidOperationException("VC targets required for closure tests.")
    ];

    private static void AssertClosureStructure(SolutionGraphDto graph)
    {
        graph.SelectedProjectName.Should().Be("ProjectD");
        graph.Nodes.Select(n => n.ProjectName).Should().BeEquivalentTo(
            ["ProjectA", "ProjectB", "ProjectC", "ProjectD"]);
        NameOrder(graph).Should().Equal("ProjectA", "ProjectB", "ProjectC", "ProjectD");
        graph.ExcludedProjects.Select(e => e.ProjectName).Should().Contain("ProjectX");
        var projectD = graph.Nodes.Single(n => n.ProjectName == "ProjectD");
        DependencyNames(graph, projectD).Should().BeEquivalentTo(["ProjectC"]);

        var projectA = graph.Nodes.Single(n => n.ProjectName == "ProjectA");
        projectA.SourceFilePaths.Should().NotBeNullOrEmpty();
        projectA.SourceFilePaths.Should().Contain(p =>
            p.EndsWith("ProjectA\\A1.cs", StringComparison.OrdinalIgnoreCase));
        projectA.SourceFilePaths.Should().NotContain(p =>
            p.EndsWith("Excluded\\Skip.cs", StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> NameOrder(SolutionGraphDto graph) =>
        graph.BuildOrder
            .Select(id => graph.Nodes.Single(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase)).ProjectName)
            .ToList();

    private static List<string> DependencyNames(SolutionGraphDto graph, GraphNodeDto node) =>
        node.Dependencies
            .Select(id => graph.Nodes.Single(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase)).ProjectName)
            .ToList();
}
