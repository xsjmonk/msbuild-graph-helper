#Requires -Version 5.1
<#
.SYNOPSIS
    Publish MSBuildGraphHelper to bin\publish-aot\<RID>\ (build only; no deploy copy).

.DESCRIPTION
    By default publishes Native AOT (PublishAot) for win-x64 into:
      .\bin\publish-aot\win-x64\MSBuildGraphHelper.exe

    This script does not copy the executable to the repo pipesim folder. Use
    build_solution_project.ps1 (Ensure-GraphHelperExecutable) or copy manually.

    Note: Native AOT is incompatible with Microsoft.Build.Locator / ProjectGraph at
    runtime (MarshalDirectiveException). For a runnable graph helper, pass -Managed.

.PARAMETER Managed
    Publish a self-contained single-file managed build instead of Native AOT.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    [string]$RuntimeIdentifier = 'win-x64',

    [switch]$Managed
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

if ($Managed) {
    Write-Host "Publishing managed self-contained single-file ($Configuration | $RuntimeIdentifier)..."
    & dotnet publish $projectFile `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -o $publishDir `
        -p:SelfContained=true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
}
else {
    Write-Host "Publishing Native AOT ($Configuration | $RuntimeIdentifier)..."
    & dotnet publish $projectFile `
        -c $Configuration `
        -r $RuntimeIdentifier `
        -o $publishDir `
        -p:PublishAot=true `
        -p:SelfContained=true
}

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
