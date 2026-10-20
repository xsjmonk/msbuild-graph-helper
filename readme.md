# MSBuildGraphHelper

Small .NET 8 console tool that evaluates a Visual Studio solution with **Microsoft.Build.Graph.ProjectGraph**, computes the **transitive prerequisite closure** for one selected project, and returns **build order** as JSON.

Typical uses: drive custom build scripts, inspect what must compile before a target, or export a dependency graph for analysis. The tool **does not compile** anything; it only evaluates MSBuild and emits JSON.

## Prerequisites

- **.NET 8 SDK** (to build this project)
- **Visual Studio 2022+** or **Build Tools** with **MSBuild**
- For solutions that contain **C++** (`.vcxproj`), the **Desktop development with C++** workload so `VCTargetsPath` and C++ MSBuild imports resolve during graph evaluation

## Build the executable

From this folder:

```powershell
.\build_msbuild_graph_helper_aot.ps1

# Output:
#   .\bin\publish-aot\win-x64\MSBuildGraphHelper.exe
```

Publishes a **framework-dependent**, **single-file** managed exe (not self-contained). Requires **.NET 8+** runtime on the machine (e.g. .NET 10). Native AOT is not supported for ProjectGraph + MSBuild.Locator.

Optional parameters on the build script: `-Configuration Release|Debug`, `-RuntimeIdentifier win-x64`.

The publish script writes only under `bin\publish-aot\<RID>\`; copy `MSBuildGraphHelper.exe` wherever your workflow needs it.

For local debugging without publishing:

```powershell
dotnet run --project MSBuildGraphHelper.csproj -- <arguments>
```

## Finding MSBuild and VCTargetsPath

The helper does not locate Visual Studio for you. You must pass paths to `--msbuild` and `--vctargets`.

**MSBuild** (prefer the **amd64** host on 64-bit Windows):

```text
<VS installation>\MSBuild\Current\Bin\amd64\MSBuild.exe
```

**VCTargetsPath** — folder that contains `Microsoft.Cpp.Default.props`:

```text
<VS installation>\MSBuild\Microsoft\VC\v170\
```

Use **vswhere** to get the installation root:

```powershell
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$install = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $install 'MSBuild\Current\Bin\amd64\MSBuild.exe'
$vcRoot = Join-Path $install 'MSBuild\Microsoft\VC'
# Prefer v170 when present (common for v143 C++ toolsets); otherwise use the newest VC version folder.
```

If graph evaluation fails with missing `Microsoft.Cpp.Default.props`, install the C++ workload or fix `--vctargets`. If evaluation runs from a directory that is not under a VS install, `VCTargetsPath` is required even for C#-only solutions that share a `.sln` with C++ projects.

## Command-line usage

```text
MSBuildGraphHelper.exe
  --solution   <path-to.sln>     (required)
  --project    <solution-name>   (required; display name from the .sln, not necessarily the .csproj file name)
  --msbuild    <path-to-MSBuild.exe>  (required)
  --vctargets  <path-to-VC-targets>   (required; folder containing Microsoft.Cpp.Default.props, e.g. ...\MSBuild\Microsoft\VC\v170\)
  [--configuration Debug|Release]     (default: Debug)
  [--platform x64]                    (default: x64)
  [--output <path-to.json>]           (optional; UTF-8 file. If omitted, JSON is written to stdout)
```

**Exit codes:** `0` success, `1` failure (message on stderr).

### Example

```powershell
$sln = 'C:\path\to\MySolution.sln'
$msbuild = 'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe'
$vc = 'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Microsoft\VC\v170\'

.\bin\publish-aot\win-x64\MSBuildGraphHelper.exe `
  --solution $sln `
  --project MyApp `
  --configuration Debug `
  --platform x64 `
  --msbuild $msbuild `
  --vctargets $vc `
  --output C:\temp\myapp-graph.json
```

Pipe stdout to a file when `--output` is omitted:

```powershell
.\MSBuildGraphHelper.exe --solution $sln --project MyApp --msbuild $msbuild --vctargets $vc > graph.json
```

`--vctargets` should be passed **without a trailing backslash** on the command line when using shells that mishandle escaped quotes (the helper normalizes the path internally).

## Input (what you pass in)

| Input | Meaning |
|-------|---------|
| **Solution path** | Full path to a `.sln` file. |
| **Project name** | Exact **solution project display name** (as in Solution Explorer / `.sln` `Project(...)` entries). Must be unique in the solution. |
| **Configuration / platform** | Solution configuration used to evaluate the graph (e.g. `Debug`, `x64`). Per-project `Configuration` and `Platform` on each node come from evaluated MSBuild properties (e.g. C# `Any CPU` mappings). |
| **MSBuild.exe** | Visual Studio’s MSBuild. Used to register an MSBuild instance via **Microsoft.Build.Locator**. |
| **VCTargetsPath** | MSVC targets directory for C++ evaluation (sets `VCTargetsPath` in global properties and the process environment during graph construction). |

Only **`.csproj`** and **`.vcxproj`** projects in the solution are included in the graph. Other project types are ignored for nodes.

## Output (JSON document)

With **`--output`**, writes one UTF-8 JSON file. Without it, the same JSON is printed to **stdout** (stderr is used for errors only).

Root object shape (`SolutionGraphDto`):

| Property | Type | Description |
|----------|------|-------------|
| `SolutionPath` | string | Normalized path to the `.sln`. |
| `SelectedProjectName` | string | The `--project` value (solution display name). |
| `RootNodeId` | string | Id of the selected project’s node in the closure. |
| `SolutionConfiguration` | string | Requested solution configuration. |
| `SolutionPlatform` | string | Requested solution platform. |
| `Nodes` | array | Projects in the **dependency closure**, sorted in build order. |
| `BuildOrder` | array of string | Same nodes as `Nodes`, listed by `Id` in topological build order (root project is last). |
| `ExcludedProjects` | array | Other `.csproj`/`.vcxproj` in the solution **not** in the closure. |

Each **node** (`GraphNodeDto`):

| Property | Description |
|----------|-------------|
| `Id` | Stable key: project path + configuration + platform. |
| `ProjectName` | File name without extension (e.g. `MyApp.Library`). |
| `ProjectPath` | Full path to the project file. |
| `ProjectType` | `Cpp` or `CSharp`. |
| `Configuration` / `Platform` | Evaluated for that node. |
| `Dependencies` | List of `Id` values for prerequisite projects within the closure. |
| `BuildEnabled` | Whether the project is marked build-enabled for the solution configuration. |
| `TopologicalIndex` | `0`-based index in the computed build order. |

Each **excluded** entry (`ExcludedProjectDto`):

| Property | Description |
|----------|-------------|
| `ProjectName` / `ProjectPath` | Project identity. |
| `Reason` | Currently `OutsideSelectedDependencyClosure` for projects not needed to build the selected target. |

### How the graph is built

1. Parse the solution and locate the selected project by **display name**.
2. Construct `ProjectGraph` with global properties (`Configuration`, `Platform`, `VCTargetsPath`).
3. Take the **prerequisite closure** of the selected project using `ProjectReference` edges.
4. Merge **solution `ProjectDependencies`** (legacy `.sln` dependency sections) into node dependencies.
5. **Topological sort** the closure; fail on cycles or if the selected project is not last in order.

Custom MSBuild targets or references that do not appear as project references or solution dependencies may be missing from the graph.

### Using the JSON in a build script

Walk `BuildOrder` (or `Nodes` by `TopologicalIndex`) and invoke MSBuild on each `ProjectPath` in sequence. When building individual projects rather than the whole solution, set `/p:BuildProjectReferences=false` so MSBuild does not rebuild the graph ahead of your schedule, and pass `/p:SolutionDir=<directory of the .sln>\` if projects rely on solution-relative paths.

## Troubleshooting

| Symptom | Likely cause |
|--------|----------------|
| `Microsoft.Cpp.Default.props` not found | Wrong or empty `--vctargets`; C++ workload not installed. |
| `No solution project named '…'` | `--project` must match the **solution display name**, not the `.csproj` file name. |
| `Ambiguous project name` | Two solution entries share the same display name; rename in the `.sln` or pick a unique solution. |
| `Failed to construct ProjectGraph` | Invalid solution mapping for `--configuration` / `--platform`, or missing SDK imports. |
| `MarshalDirectiveException` / stack buffer overrun | Old **Native AOT** publish; rebuild with **`build_msbuild_graph_helper_aot.ps1`** (framework-dependent single-file). |
| `MSBuildLocator found no Visual Studio MSBuild instances` | `--msbuild` must point to a Visual Studio MSBuild, not a standalone copy without a registered instance. |

## Source layout

| File | Role |
|------|------|
| `Program.cs` | CLI parsing, JSON serialization |
| `MsBuildHost.cs` | MSBuild.Locator registration |
| `GraphExtractor.cs` | ProjectGraph, closure, sort, exclusions |
| `Models.cs` | DTO types for JSON |
| `build_msbuild_graph_helper_aot.ps1` | Publish script (`-Managed` vs Native AOT) |
