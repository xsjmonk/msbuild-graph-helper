using System.Collections.Generic;

namespace MSBuildGraphHelper;

public sealed class GraphNodeDto
{
    public string Id { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string ProjectType { get; set; } = "";
    public string Configuration { get; set; } = "";
    public string Platform { get; set; } = "";
    public List<string> Dependencies { get; set; } = new();
    public bool BuildEnabled { get; set; }
    public int TopologicalIndex { get; set; }
    public List<string> SourceFilePaths { get; set; } = new();
}

public sealed class ExcludedProjectDto
{
    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string Reason { get; set; } = "";
    public List<string> SourceFilePaths { get; set; } = new();
}

public sealed class ProjectSourceFilesEntryDto
{
    public string ProjectName { get; set; } = "";
    public string ProjectPath { get; set; } = "";
    public string ProjectType { get; set; } = "";
    public string Configuration { get; set; } = "";
    public string Platform { get; set; } = "";
    public List<string> SourceFilePaths { get; set; } = new();
}

/// <summary>
/// Standalone output for --mode source-files: projects and evaluated source paths in a solution.
/// </summary>
public sealed class SolutionSourceFilesDto
{
    public string SolutionPath { get; set; } = "";
    public string? SelectedProjectName { get; set; }
    public string SolutionConfiguration { get; set; } = "";
    public string SolutionPlatform { get; set; } = "";
    public List<ProjectSourceFilesEntryDto> Projects { get; set; } = new();
}

public sealed class SolutionGraphDto
{
    public string SolutionPath { get; set; } = "";
    public string SelectedProjectName { get; set; } = "";
    public string RootNodeId { get; set; } = "";
    public string SolutionConfiguration { get; set; } = "";
    public string SolutionPlatform { get; set; } = "";
    public List<GraphNodeDto> Nodes { get; set; } = new();
    public List<string> BuildOrder { get; set; } = new();
    public List<ExcludedProjectDto> ExcludedProjects { get; set; } = new();
}
