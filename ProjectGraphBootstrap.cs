using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Graph;
using SolutionProject = Microsoft.Build.Construction.ProjectInSolution;

namespace MSBuildGraphHelper;

internal static class ProjectGraphBootstrap
{
    internal static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vcxproj", ".csproj"
    };

    internal sealed class Context
    {
        public required string SolutionPath { get; init; }
        public required SolutionFile Solution { get; init; }
        public required ProjectGraph Graph { get; init; }
        public required IReadOnlyList<SolutionProject> BuildableSolutionProjects { get; init; }
    }

    internal static Context Open(
        string solutionPath,
        string configuration,
        string solutionPlatform,
        string vcTargetsPath)
    {
        solutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(solutionPath))
        {
            throw new InvalidOperationException($"Solution not found: {solutionPath}");
        }

        vcTargetsPath = MsBuildVcTargets.Normalize(
            vcTargetsPath,
            "VCTargetsPath is required for solutions that contain C++ (.vcxproj) projects.");
        Environment.SetEnvironmentVariable("VCTargetsPath", vcTargetsPath);

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = configuration,
            ["Platform"] = solutionPlatform,
            ["VCTargetsPath"] = vcTargetsPath
        };
        MsBuildSolutionDir.ApplyToGlobalProperties(globalProperties, solutionPath);

        ProjectGraph graph;
        try
        {
            graph = new ProjectGraph(solutionPath, globalProperties);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to construct ProjectGraph: {ex.Message}", ex);
        }

        var solution = SolutionFile.Parse(solutionPath);
        var buildable = solution.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .Where(p => SupportedExtensions.Contains(Path.GetExtension(Path.GetFullPath(p.AbsolutePath))))
            .ToList();

        return new Context
        {
            SolutionPath = solutionPath,
            Solution = solution,
            Graph = graph,
            BuildableSolutionProjects = buildable
        };
    }

    internal static ProjectGraphNode? FindEvaluatedNodeForProjectPath(ProjectGraph graph, string projectPath)
    {
        var fullPath = Path.GetFullPath(projectPath);
        return graph.ProjectNodes.FirstOrDefault(n =>
            string.Equals(Path.GetFullPath(n.ProjectInstance.FullPath), fullPath, StringComparison.OrdinalIgnoreCase));
    }
}
