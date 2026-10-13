using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Construction;
using Microsoft.Build.Graph;

namespace MSBuildGraphHelper;

public static class GraphExtractor
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".vcxproj", ".csproj"
    };

    public static SolutionGraphDto Extract(
        string solutionPath,
        string projectName,
        string configuration,
        string solutionPlatform,
        string vcTargetsPath)
    {
        solutionPath = Path.GetFullPath(solutionPath);
        if (!File.Exists(solutionPath))
        {
            throw new InvalidOperationException($"Solution not found: {solutionPath}");
        }

        var solution = SolutionFile.Parse(solutionPath);
        var solutionProjects = solution.ProjectsInOrder
            .Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat)
            .ToList();

        var nameMatches = solutionProjects
            .Where(p => string.Equals(p.ProjectName, projectName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nameMatches.Count == 0)
        {
            var valid = solutionProjects.Select(p => p.ProjectName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException(
                $"No solution project named '{projectName}'. Valid buildable names include: {string.Join(", ", valid)}");
        }

        if (nameMatches.Count > 1)
        {
            var paths = string.Join(Environment.NewLine, nameMatches.Select(p => $"  - {p.AbsolutePath}"));
            throw new InvalidOperationException(
                $"Ambiguous project name '{projectName}'. Multiple projects share this display name:{Environment.NewLine}{paths}");
        }

        var selectedSolutionProject = nameMatches[0];
        var selectedPath = Path.GetFullPath(selectedSolutionProject.AbsolutePath);
        if (!SupportedExtensions.Contains(Path.GetExtension(selectedPath)))
        {
            throw new InvalidOperationException(
                $"Project '{projectName}' has unsupported type '{Path.GetExtension(selectedPath)}'. Supported: .vcxproj, .csproj");
        }

        vcTargetsPath = NormalizeVcTargetsPath(vcTargetsPath);
        Environment.SetEnvironmentVariable("VCTargetsPath", vcTargetsPath);

        var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Configuration"] = configuration,
            ["Platform"] = solutionPlatform,
            ["VCTargetsPath"] = vcTargetsPath
        };

        ProjectGraph graph;
        try
        {
            graph = new ProjectGraph(solutionPath, globalProperties);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to construct ProjectGraph: {ex.Message}", ex);
        }

        var allNodes = graph.ProjectNodes.ToList();
        var nodeById = new Dictionary<string, ProjectGraphNode>(StringComparer.OrdinalIgnoreCase);
        var dtoByNode = new Dictionary<ProjectGraphNode, GraphNodeDto>();

        foreach (var node in allNodes)
        {
            var path = Path.GetFullPath(node.ProjectInstance.FullPath);
            var ext = Path.GetExtension(path);
            if (!SupportedExtensions.Contains(ext))
            {
                continue;
            }

            var nodeConfig = GetProperty(node, "Configuration") ?? configuration;
            var nodePlatform = GetProperty(node, "Platform") ?? solutionPlatform;
            var id = BuildNodeId(path, nodeConfig, nodePlatform);
            if (nodeById.ContainsKey(id))
            {
                continue;
            }

            var dto = new GraphNodeDto
            {
                Id = id,
                ProjectName = Path.GetFileNameWithoutExtension(path),
                ProjectPath = path,
                ProjectType = ext.Equals(".vcxproj", StringComparison.OrdinalIgnoreCase) ? "Cpp" : "CSharp",
                Configuration = nodeConfig,
                Platform = nodePlatform,
                BuildEnabled = IsBuildEnabled(solution, path, configuration, solutionPlatform)
            };
            nodeById[id] = node;
            dtoByNode[node] = dto;
        }

        var rootCandidates = dtoByNode.Values
            .Where(n => string.Equals(n.ProjectPath, selectedPath, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (rootCandidates.Count == 0)
        {
            throw new InvalidOperationException(
                $"Selected project '{projectName}' was not found in the evaluated ProjectGraph for {configuration}|{solutionPlatform}.");
        }

        if (rootCandidates.Count > 1)
        {
            var detail = string.Join(Environment.NewLine, rootCandidates.Select(n => $"  - {n.Id}"));
            throw new InvalidOperationException(
                $"Multiple evaluated graph nodes match project '{projectName}'. Specify a more specific solution mapping or resolve ambiguity:{Environment.NewLine}{detail}");
        }

        var rootDto = rootCandidates[0];
        var rootNode = nodeById[rootDto.Id];

        MergeSolutionDependencies(solution, solutionPath, configuration, solutionPlatform, allNodes, dtoByNode);

        var closureNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var closureNodes = new List<ProjectGraphNode>();
        CollectPrerequisiteClosure(graph, rootNode, dtoByNode, closureNodeIds, closureNodes);

        foreach (var node in closureNodes)
        {
            if (!dtoByNode.TryGetValue(node, out var dto))
            {
                continue;
            }

            var depIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var refNode in GetReferencedProjectNodes(graph, node).Where(dtoByNode.ContainsKey))
            {
                var refId = dtoByNode[refNode].Id;
                if (closureNodeIds.Contains(refId))
                {
                    depIds.Add(refId);
                }
            }

            foreach (var existing in dto.Dependencies.Where(closureNodeIds.Contains))
            {
                depIds.Add(existing);
            }

            dto.Dependencies = depIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var closureDtos = closureNodes
            .Select(n => dtoByNode[n])
            .ToList();

        DetectCycle(closureDtos);

        var sorted = TopologicalSort(closureDtos);
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].TopologicalIndex = i;
        }

        var buildOrder = sorted.Select(n => n.Id).ToList();
        if (buildOrder.Count == 0 || !string.Equals(buildOrder[^1], rootDto.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Topological sort did not place the selected root project last. The dependency graph may be invalid.");
        }

        // MSBuild's topological order reflects ProjectReference edges only; we also merge
        // solution ProjectDependencies, so relative order may legitimately differ.
        WarnIfBuildOrderViolatesMsBuildReferenceOrder(buildOrder, graph, dtoByNode, closureNodeIds);

        var excluded = solutionProjects
            .Where(p => !string.Equals(Path.GetFullPath(p.AbsolutePath), selectedPath, StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(p.AbsolutePath))
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !closureDtos.Any(n => string.Equals(n.ProjectPath, path, StringComparison.OrdinalIgnoreCase)))
            .Select(path => new ExcludedProjectDto
            {
                ProjectName = Path.GetFileNameWithoutExtension(path),
                ProjectPath = path,
                Reason = "OutsideSelectedDependencyClosure"
            })
            .OrderBy(e => e.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SolutionGraphDto
        {
            SolutionPath = solutionPath,
            SelectedProjectName = projectName,
            RootNodeId = rootDto.Id,
            SolutionConfiguration = configuration,
            SolutionPlatform = solutionPlatform,
            Nodes = sorted,
            BuildOrder = buildOrder,
            ExcludedProjects = excluded
        };
    }

    private static void MergeSolutionDependencies(
        SolutionFile solution,
        string solutionPath,
        string configuration,
        string solutionPlatform,
        List<ProjectGraphNode> allNodes,
        Dictionary<ProjectGraphNode, GraphNodeDto> dtoByNode)
    {
        var solutionDeps = ReadSolutionProjectDependencies(solutionPath);
        foreach (var sp in solution.ProjectsInOrder.Where(p => p.ProjectType == SolutionProjectType.KnownToBeMSBuildFormat))
        {
            var dependentPath = Path.GetFullPath(sp.AbsolutePath);
            if (!SupportedExtensions.Contains(Path.GetExtension(dependentPath)))
            {
                continue;
            }

            if (!solutionDeps.TryGetValue(sp.ProjectGuid, out var depGuids) || depGuids.Count == 0)
            {
                continue;
            }

            foreach (var depGuid in depGuids)
            {
                var depProject = solution.ProjectsInOrder.FirstOrDefault(p =>
                    string.Equals(p.ProjectGuid, depGuid, StringComparison.OrdinalIgnoreCase));
                if (depProject == null || depProject.ProjectType != SolutionProjectType.KnownToBeMSBuildFormat)
                {
                    continue;
                }

                var dependencyPath = Path.GetFullPath(depProject.AbsolutePath);
                if (!SupportedExtensions.Contains(Path.GetExtension(dependencyPath)))
                {
                    throw new InvalidOperationException(
                        $"Solution dependency from '{sp.ProjectName}' requires unsupported project '{depProject.ProjectName}' ({dependencyPath}).");
                }

                var dependentNode = FindNodeForPath(allNodes, dtoByNode, dependentPath, configuration, solutionPlatform);
                var dependencyNode = FindNodeForPath(allNodes, dtoByNode, dependencyPath, configuration, solutionPlatform);
                if (dependentNode == null || dependencyNode == null)
                {
                    continue;
                }

                var dependentDto = dtoByNode[dependentNode];
                var dependencyDto = dtoByNode[dependencyNode];
                if (!dependentDto.Dependencies.Contains(dependencyDto.Id, StringComparer.OrdinalIgnoreCase))
                {
                    dependentDto.Dependencies.Add(dependencyDto.Id);
                }
            }
        }
    }

    private static ProjectGraphNode? FindNodeForPath(
        List<ProjectGraphNode> allNodes,
        Dictionary<ProjectGraphNode, GraphNodeDto> dtoByNode,
        string projectPath,
        string configuration,
        string solutionPlatform)
    {
        var matches = allNodes
            .Where(n => string.Equals(Path.GetFullPath(n.ProjectInstance.FullPath), projectPath, StringComparison.OrdinalIgnoreCase))
            .Where(n => dtoByNode.ContainsKey(n))
            .ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        return matches.FirstOrDefault(n =>
        {
            var dto = dtoByNode[n];
            return string.Equals(dto.Configuration, configuration, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(dto.Platform, solutionPlatform, StringComparison.OrdinalIgnoreCase);
        }) ?? matches.FirstOrDefault();
    }

    private static void CollectPrerequisiteClosure(
        ProjectGraph graph,
        ProjectGraphNode root,
        Dictionary<ProjectGraphNode, GraphNodeDto> dtoByNode,
        HashSet<string> closureIds,
        List<ProjectGraphNode> closureNodes)
    {
        var stack = new Stack<ProjectGraphNode>();
        var visited = new HashSet<ProjectGraphNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            if (!dtoByNode.ContainsKey(current))
            {
                var path = current.ProjectInstance.FullPath;
                throw new InvalidOperationException(
                    $"Required dependency '{path}' is not a supported buildable project type in the evaluated graph.");
            }

            foreach (var dep in GetReferencedProjectNodes(graph, current).Where(dtoByNode.ContainsKey))
            {
                stack.Push(dep);
            }
        }

        foreach (var node in visited.OrderBy(n => dtoByNode[n].Id, StringComparer.OrdinalIgnoreCase))
        {
            closureIds.Add(dtoByNode[node].Id);
            closureNodes.Add(node);
        }
    }

    private static void DetectCycle(List<GraphNodeDto> nodes)
    {
        var idSet = nodes.Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var path = new List<string>();

        foreach (var node in nodes.OrderBy(n => n.Id, StringComparer.OrdinalIgnoreCase))
        {
            if (Visit(node.Id, nodes, idSet, state, path))
            {
                throw new InvalidOperationException($"Dependency cycle detected: {string.Join(" -> ", path)}");
            }
        }
    }

    private static bool Visit(
        string id,
        List<GraphNodeDto> nodes,
        HashSet<string> idSet,
        Dictionary<string, int> state,
        List<string> path)
    {
        if (!state.TryGetValue(id, out var s))
        {
            state[id] = 1;
            path.Add(id);
            var node = nodes.First(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));
            foreach (var dep in node.Dependencies.Where(idSet.Contains))
            {
                if (Visit(dep, nodes, idSet, state, path))
                {
                    return true;
                }
            }

            path.RemoveAt(path.Count - 1);
            state[id] = 2;
            return false;
        }

        if (s == 1)
        {
            path.Add(id);
            return true;
        }

        return false;
    }

    private static List<GraphNodeDto> TopologicalSort(List<GraphNodeDto> nodes)
    {
        var byId = nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);
        var incoming = nodes.ToDictionary(
            n => n.Id,
            n => n.Dependencies.Count(dep => byId.ContainsKey(dep)),
            StringComparer.OrdinalIgnoreCase);

        var queue = new SortedSet<string>(incoming.Where(kv => kv.Value == 0).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
        var result = new List<GraphNodeDto>();

        while (queue.Count > 0)
        {
            var id = queue.Min;
            queue.Remove(id);
            var node = byId[id];
            result.Add(node);

            foreach (var dependent in nodes.Where(n => n.Dependencies.Contains(id, StringComparer.OrdinalIgnoreCase)))
            {
                incoming[dependent.Id]--;
                if (incoming[dependent.Id] == 0)
                {
                    queue.Add(dependent.Id);
                }
            }
        }

        if (result.Count != nodes.Count)
        {
            throw new InvalidOperationException("Failed to produce a complete topological ordering (cycle or missing edge).");
        }

        return result;
    }

    private static void WarnIfBuildOrderViolatesMsBuildReferenceOrder(
        List<string> buildOrder,
        ProjectGraph graph,
        Dictionary<ProjectGraphNode, GraphNodeDto> dtoByNode,
        HashSet<string> closureNodeIds)
    {
        var crossCheck = graph.ProjectNodesTopologicallySorted
            .Where(n => dtoByNode.ContainsKey(n) && closureNodeIds.Contains(dtoByNode[n].Id))
            .Select(n => dtoByNode[n].Id)
            .ToList();
        if (crossCheck.Count == 0)
        {
            return;
        }

        var index = buildOrder
            .Select((id, i) => new { id, i })
            .ToDictionary(x => x.id, x => x.i, StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < crossCheck.Count - 1; i++)
        {
            for (var j = i + 1; j < crossCheck.Count; j++)
            {
                if (!index.TryGetValue(crossCheck[i], out var a) || !index.TryGetValue(crossCheck[j], out var b))
                {
                    continue;
                }

                if (a > b)
                {
                    Console.Error.WriteLine(
                        "Warning: build order differs from ProjectGraph.ProjectNodesTopologicallySorted (often due to solution ProjectDependencies).");
                    return;
                }
            }
        }
    }

    private static bool IsBuildEnabled(SolutionFile solution, string projectPath, string configuration, string solutionPlatform)
    {
        var project = solution.ProjectsInOrder.FirstOrDefault(p =>
            string.Equals(Path.GetFullPath(p.AbsolutePath), projectPath, StringComparison.OrdinalIgnoreCase));
        if (project == null)
        {
            return true;
        }

        var configKey = $"{configuration}|{solutionPlatform}";
        if (project.ProjectConfigurations.TryGetValue(configKey, out var mapping))
        {
            return mapping.IncludeInBuild;
        }

        return true;
    }

    private static string? GetProperty(ProjectGraphNode node, string name)
    {
        return node.ProjectInstance.GetPropertyValue(name);
    }

    private static IEnumerable<ProjectGraphNode> GetReferencedProjectNodes(ProjectGraph graph, ProjectGraphNode node)
    {
        var projectDirectory = Path.GetDirectoryName(node.ProjectInstance.FullPath) ?? string.Empty;
        var referencedPaths = node.ProjectInstance.GetItems("ProjectReference")
            .Select(item => Path.GetFullPath(Path.Combine(projectDirectory, item.EvaluatedInclude)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return graph.ProjectNodes.Where(candidate =>
            referencedPaths.Contains(Path.GetFullPath(candidate.ProjectInstance.FullPath)));
    }

    private static Dictionary<string, List<string>> ReadSolutionProjectDependencies(string solutionPath)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(solutionPath);
        string? sectionOwner = null;
        var inDependencySection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.StartsWith("Project(", StringComparison.Ordinal))
            {
                sectionOwner = null;
                inDependencySection = false;
                var guidMatch = Regex.Match(line, @"\{([0-9A-Fa-f-]{36})\}");
                if (guidMatch.Success)
                {
                    sectionOwner = guidMatch.Groups[1].Value;
                }

                continue;
            }

            if (line.StartsWith("ProjectSection(ProjectDependencies)", StringComparison.OrdinalIgnoreCase))
            {
                inDependencySection = !string.IsNullOrWhiteSpace(sectionOwner);
                if (inDependencySection && sectionOwner != null && !result.ContainsKey(sectionOwner))
                {
                    result[sectionOwner] = new List<string>();
                }

                continue;
            }

            if (line.StartsWith("EndProjectSection", StringComparison.OrdinalIgnoreCase))
            {
                inDependencySection = false;
                continue;
            }

            if (!inDependencySection || sectionOwner == null)
            {
                continue;
            }

            var depMatch = Regex.Match(line, @"\{([0-9A-Fa-f-]{36})\}\s*=\s*\{([0-9A-Fa-f-]{36})\}");
            if (depMatch.Success)
            {
                result[sectionOwner].Add(depMatch.Groups[2].Value);
            }
        }

        return result;
    }

    private static string BuildNodeId(string path, string configuration, string platform)
    {
        return $"{Path.GetFullPath(path)}|{configuration}|{platform}".ToUpperInvariant();
    }

    private static string NormalizeVcTargetsPath(string vcTargetsPath)
    {
        if (string.IsNullOrWhiteSpace(vcTargetsPath))
        {
            throw new InvalidOperationException("VCTargetsPath is required for solutions that contain C++ (.vcxproj) projects.");
        }

        var normalized = Path.GetFullPath(vcTargetsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        normalized += Path.DirectorySeparatorChar;

        var defaultProps = Path.Combine(normalized, "Microsoft.Cpp.Default.props");
        if (!File.Exists(defaultProps))
        {
            throw new InvalidOperationException($"VCTargetsPath is invalid (missing Microsoft.Cpp.Default.props): {normalized}");
        }

        return normalized;
    }
}
