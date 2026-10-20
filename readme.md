# MSBuildGraphHelper

Small .NET 8 console tool that **evaluates** a Visual Studio solution with **Microsoft.Build.Graph.ProjectGraph** and returns JSON. It **does not compile** anything.

For every relevant project it reports:

- **`ProjectPath`** — full path to the `.csproj` or `.vcxproj`
- **`SourceFilePaths`** — full physical paths of evaluated source inputs (same information as MSBuild **`-getItem:Compile`** / **`ClCompile`** / **`ClInclude`**)

Typical uses: dependency-aware builds, **which project owns this `.cs` file?**, and exporting a solution’s project/file map for tooling.

## Modes

| `--mode` | Default | `--project` | Output |
|----------|---------|-------------|--------|
| **`source-files`** | | Optional | **`SolutionSourceFilesDto`**: every (or one) buildable project with **`ProjectPath`** + **`SourceFilePaths`** |
| **`closure`** | Yes | **Required** | **`SolutionGraphDto`**: dependency closure, **`BuildOrder`**, plus **`SourceFilePaths`** on each node |

Shared arguments for both modes:

| Argument | Required | Purpose |
|----------|----------|---------|
| `--solution` | Yes | Path to the `.sln` |
| `--msbuild` | Yes | Visual Studio `MSBuild.exe` (prefer amd64 on 64-bit Windows) |
| `--vctargets` | Yes | VC targets folder with `Microsoft.Cpp.Default.props` when the solution has C++ |
| `--configuration` | No | `Debug` (default) or `Release` |
| `--platform` | No | `x64` (default) |
| `--output` | No | UTF-8 JSON file; otherwise **stdout** |

Evaluation uses **ProjectGraph** (`Configuration`, `Platform`, `VCTargetsPath`, `SolutionDir`). Source paths are read from the evaluated project instance (same data as MSBuild **`-getItem:Compile`** / **`ClCompile`** / **`ClInclude`**).

---

## Standalone: `--mode source-files`

Use this when you only need **which project owns which files** — no dependency closure or build order.

### Whole solution

Omit **`--project`**. JSON lists **every** buildable `.csproj` / `.vcxproj` in the solution.

```powershell
.\MSBuildGraphHelper.exe `
  --mode source-files `
  --solution C:\path\MySolution.sln `
  --msbuild $msbuild `
  --vctargets $vc `
  --output C:\temp\solution-files.json
```

### Single project

Pass **`--project`** with the **solution display name** (Solution Explorer name, not necessarily the `.csproj` file name).

```powershell
.\MSBuildGraphHelper.exe `
  --mode source-files `
  --solution C:\path\Stingray.sln `
  --project StandardDomain `
  --msbuild $msbuild `
  --vctargets $vc
```

### `source-files` JSON (`SolutionSourceFilesDto`)

| Property | Meaning |
|----------|---------|
| `SolutionPath` | Normalized `.sln` path |
| `SelectedProjectName` | `--project` when filtering to one project; otherwise `null` |
| `SolutionConfiguration` / `SolutionPlatform` | Evaluation mapping |
| `Projects` | Array of entries below |

Each **`Projects[]`** entry (`ProjectSourceFilesEntryDto`):

| Property | Meaning |
|----------|---------|
| `ProjectName` | Solution display name |
| `ProjectPath` | Full path to `.csproj` or `.vcxproj` |
| `ProjectType` | `CSharp` or `Cpp` |
| `Configuration` / `Platform` | Evaluated for that project |
| `SourceFilePaths` | Full physical paths of evaluated source inputs |

**Find file owner:** search all `Projects[].SourceFilePaths` for your full path (case-insensitive); use that row’s `ProjectPath` / `ProjectName`.

Example fragment:

```json
{
  "SolutionPath": "C:\\...\\Stingray.sln",
  "SelectedProjectName": "StandardDomain",
  "Projects": [
    {
      "ProjectName": "StandardDomain",
      "ProjectPath": "C:\\...\\StandardDomain.csproj",
      "SourceFilePaths": [
        "C:\\...\\ForecastingMbxStepInputWriter.cs"
      ]
    }
  ]
}
```

---

## Dependency closure: `--mode closure` (default)

Used by **`build_solution_project.ps1`**. Requires **`--project`** (closure root). Returns **`SolutionGraphDto`** with **`Nodes`**, **`BuildOrder`**, **`ExcludedProjects`**, and **`SourceFilePaths`** on closure and excluded projects.

```powershell
.\MSBuildGraphHelper.exe `
  --solution C:\path\Stingray.sln `
  --project Engineering.Desktop `
  --msbuild $msbuild `
  --vctargets $vc
```

(`--mode closure` is optional because it is the default.)

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

## Command-line reference

```text
MSBuildGraphHelper.exe
  [--mode closure|source-files]       (default: closure)
  --solution   <path-to.sln>          (required)
  [--project   <solution-display-name>] (required for closure; optional for source-files)
  --msbuild    <path-to-MSBuild.exe>  (required)
  --vctargets  <path-to-VC-targets>   (required when solution contains C++)
  [--configuration Debug|Release]     (default: Debug)
  [--platform x64]                    (default: x64)
  [--output <path-to.json>]           (optional; UTF-8 file, else stdout)
```

**Exit codes:** `0` success, `1` failure (message on stderr).

`--vctargets` should be passed **without a trailing backslash** on the command line when using shells that mishandle escaped quotes (the helper normalizes the path internally).

Only **`.csproj`** and **`.vcxproj`** projects in the solution are included. Other project types are omitted.

## JSON reference

| Root property | Description |
|---------------|-------------|
| `SolutionPath` | Normalized `.sln` path |
| `SelectedProjectName` | `--project` value |
| `RootNodeId` | Graph id of the selected project |
| `SolutionConfiguration` / `SolutionPlatform` | Requested solution mapping |
| `Nodes` | Closure projects: **`ProjectPath`**, **`SourceFilePaths`**, dependencies, build metadata |
| `BuildOrder` | `Nodes` ids in compile order (selected project last) |
| `ExcludedProjects` | Other solution projects: **`ProjectPath`**, **`SourceFilePaths`**, `Reason` |

### How the graph is built

1. Parse the solution; resolve **`--project`** by solution **display name**.
2. Construct **`ProjectGraph`** (`Configuration`, `Platform`, `VCTargetsPath`, `SolutionDir`).
3. Collect **`SourceFilePaths`** from each evaluated **`ProjectInstance`** (MSBuild item API).
4. Compute the **prerequisite closure** of the selected project (`ProjectReference` + `.sln` `ProjectDependencies`).
5. **Topological sort**; attach file lists to closure **`Nodes`** and to **`ExcludedProjects`**.

Custom targets or references not visible as project references or solution dependencies may be missing from the graph or file lists.

### Using the JSON in a build script

Walk **`BuildOrder`** and invoke MSBuild on each node’s **`ProjectPath`**. Use **`/p:BuildProjectReferences=false`** and **`/p:SolutionDir=<directory of the .sln>\`** when building projects individually. Match changed files using **`SourceFilePaths`** on **`Nodes`** and **`ExcludedProjects`**.

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
| `GraphExtractor.cs` | `--mode closure`: ProjectGraph, closure, sort, exclusions |
| `SolutionSourceFileCatalog.cs` | `--mode source-files`: solution / project file lists |
| `HelperRunner.cs` / `CliParser.cs` | Mode dispatch and CLI parsing |
| `Models.cs` | DTO types for JSON |
| `ProjectEvaluatedSourceFiles.cs` | Evaluated `Compile` / `ClCompile` / `ClInclude` → full paths |
| `build_msbuild_graph_helper_aot.ps1` | Publish framework-dependent single-file exe |
