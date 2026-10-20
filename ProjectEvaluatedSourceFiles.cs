using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Execution;
using Microsoft.Build.Graph;

namespace MSBuildGraphHelper;

/// <summary>
/// Collects evaluated source inputs from an already-loaded <see cref="ProjectGraphNode"/>.
/// Uses the same evaluated project instance as ProjectGraph (equivalent to MSBuild -getItem).
/// </summary>
internal static class ProjectEvaluatedSourceFiles
{
    private static readonly string[] CSharpItemTypes = ["Compile"];
    private static readonly string[] CppItemTypes = ["ClCompile", "ClInclude"];

    public static List<string> Collect(ProjectGraphNode node, string projectType)
    {
        var itemTypes = string.Equals(projectType, "Cpp", StringComparison.OrdinalIgnoreCase)
            ? CppItemTypes
            : CSharpItemTypes;

        var instance = node.ProjectInstance;
        var projectDirectory = Path.GetDirectoryName(instance.FullPath) ?? string.Empty;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var itemType in itemTypes)
        {
            foreach (var item in instance.GetItems(itemType))
            {
                var fullPath = ResolveItemFullPath(item, projectDirectory);
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    paths.Add(fullPath);
                }
            }
        }

        return paths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string ResolveItemFullPath(ProjectItemInstance item, string projectDirectory)
    {
        var fullPathMetadata = item.GetMetadataValue("FullPath");
        if (!string.IsNullOrWhiteSpace(fullPathMetadata))
        {
            return Path.GetFullPath(fullPathMetadata);
        }

        var include = item.EvaluatedInclude;
        if (string.IsNullOrWhiteSpace(include))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(include))
        {
            return Path.GetFullPath(include);
        }

        return Path.GetFullPath(Path.Combine(projectDirectory, include));
    }
}
