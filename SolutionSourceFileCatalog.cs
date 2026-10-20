using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Graph;

namespace MSBuildGraphHelper;

/// <summary>
/// Standalone feature: list each solution project's path and evaluated source file paths (MSBuild items).
/// </summary>
public static class SolutionSourceFileCatalog
{
    public static SolutionSourceFilesDto ExtractSolution(
        string solutionPath,
        string configuration,
        string solutionPlatform,
        string vcTargetsPath)
    {
        return Extract(solutionPath, configuration, solutionPlatform, vcTargetsPath, projectDisplayNameFilter: null);
    }

    public static SolutionSourceFilesDto ExtractProject(
        string solutionPath,
        string projectDisplayName,
        string configuration,
        string solutionPlatform,
        string vcTargetsPath)
    {
        if (string.IsNullOrWhiteSpace(projectDisplayName))
        {
            throw new InvalidOperationException("Project display name is required.");
        }

        return Extract(solutionPath, configuration, solutionPlatform, vcTargetsPath, projectDisplayNameFilter: projectDisplayName);
    }

    private static SolutionSourceFilesDto Extract(
        string solutionPath,
        string configuration,
        string solutionPlatform,
        string vcTargetsPath,
        string? projectDisplayNameFilter)
    {
        var context = ProjectGraphBootstrap.Open(solutionPath, configuration, solutionPlatform, vcTargetsPath);

        if (!string.IsNullOrWhiteSpace(projectDisplayNameFilter))
        {
            var matches = context.BuildableSolutionProjects
                .Where(p => string.Equals(p.ProjectName, projectDisplayNameFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0)
            {
                var valid = context.BuildableSolutionProjects.Select(p => p.ProjectName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
                throw new InvalidOperationException(
                    $"No solution project named '{projectDisplayNameFilter}'. Valid buildable names include: {string.Join(", ", valid)}");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Ambiguous project name '{projectDisplayNameFilter}'.");
            }
        }

        var projects = new List<ProjectSourceFilesEntryDto>();
        foreach (var solutionProject in context.BuildableSolutionProjects)
        {
            if (!string.IsNullOrWhiteSpace(projectDisplayNameFilter)
                && !string.Equals(solutionProject.ProjectName, projectDisplayNameFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var projectPath = Path.GetFullPath(solutionProject.AbsolutePath);
            var graphNode = ProjectGraphBootstrap.FindEvaluatedNodeForProjectPath(context.Graph, projectPath);
            if (graphNode is null)
            {
                throw new InvalidOperationException(
                    $"Project '{solutionProject.ProjectName}' was not found in the evaluated ProjectGraph: {projectPath}");
            }

            var ext = Path.GetExtension(projectPath);
            var projectType = ext.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase) ? "Cpp" : "CSharp";
            var configurationOnNode = graphNode.ProjectInstance.GetPropertyValue("Configuration") ?? configuration;
            var platformOnNode = graphNode.ProjectInstance.GetPropertyValue("Platform") ?? solutionPlatform;

            projects.Add(new ProjectSourceFilesEntryDto
            {
                ProjectName = solutionProject.ProjectName,
                ProjectPath = projectPath,
                ProjectType = projectType,
                Configuration = configurationOnNode,
                Platform = platformOnNode,
                SourceFilePaths = ProjectEvaluatedSourceFiles.Collect(graphNode, projectType)
            });
        }

        projects = projects.OrderBy(p => p.ProjectName, StringComparer.OrdinalIgnoreCase).ToList();

        return new SolutionSourceFilesDto
        {
            SolutionPath = context.SolutionPath,
            SelectedProjectName = projectDisplayNameFilter,
            SolutionConfiguration = configuration,
            SolutionPlatform = solutionPlatform,
            Projects = projects
        };
    }
}
