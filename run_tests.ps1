#Requires -Version 5.1
<#
.SYNOPSIS
    Build and run all MSBuildGraphHelper unit and integration tests.

.DESCRIPTION
    Restores, builds, and executes the xUnit test project under
    MSBuildGraphHelper.Tests. Exits with the same code as dotnet test (0 = all passed).

.EXAMPLE
    .\run_tests.ps1
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectDir = $PSScriptRoot
$testProject = Join-Path $projectDir 'MSBuildGraphHelper.Tests\MSBuildGraphHelper.Tests.csproj'

if (-not (Test-Path -LiteralPath $testProject)) {
    Write-Error "Test project not found: $testProject"
    exit 1
}

Write-Host "Running tests: $testProject"
& dotnet test $testProject

if ($LASTEXITCODE -ne 0) {
    Write-Error "Tests failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}

Write-Host 'All tests passed.'
exit 0
