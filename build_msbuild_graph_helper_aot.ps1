#Requires -Version 5.1
<#
.SYNOPSIS
    Publish MSBuildGraphHelper to bin\publish-aot\<RID>\ (build only; no deploy copy).

.DESCRIPTION
    Publishes a framework-dependent, single-file managed executable for win-x64 into:
      .\bin\publish-aot\win-x64\MSBuildGraphHelper.exe

    Requires a matching .NET runtime on the machine (project targets net8.0; .NET 8+ or .NET 10 is fine).

    This script does not copy the executable to the repo pipesim folder. Use
    build_solution_project.ps1 (Ensure-GraphHelperExecutable) or copy manually.

    Native AOT is not used (ProjectGraph + MSBuild.Locator need a managed host).
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$RuntimeIdentifier = 'win-x64'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectDir = $PSScriptRoot
$projectFile = Join-Path $projectDir 'MSBuildGraphHelper.csproj'
$publishDir = Join-Path $projectDir "bin\publish-aot\$RuntimeIdentifier"

if (-not (Test-Path -LiteralPath $projectFile)) {
    Write-Error "Project not found: $projectFile"
    exit 1
}

Write-Host "Publishing framework-dependent single-file ($Configuration | $RuntimeIdentifier)..."
& dotnet publish $projectFile `
    -c $Configuration `
    -r $RuntimeIdentifier `
    -o $publishDir `
    -p:SelfContained=false `
    -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE."
    exit $LASTEXITCODE
}

$publishedExe = Join-Path $publishDir 'MSBuildGraphHelper.exe'
if (-not (Test-Path -LiteralPath $publishedExe)) {
    Write-Error "Published executable not found: $publishedExe"
    exit 1
}

Write-Host "Published: $publishedExe"
Write-Host "Publish folder: $publishDir"

exit 0
