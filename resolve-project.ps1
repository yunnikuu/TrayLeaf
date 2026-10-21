[CmdletBinding()]
param(
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# This script is the single project-root resolver. It intentionally ignores the
# caller's current directory and any Git auto-discovery.
$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$required = @('src', 'tests', 'build.ps1', 'build-release.ps1')
$missing = @(
    $required | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $projectRoot $_))
    }
)

if ($missing.Count -gt 0) {
    throw "TrayLeaf project root is invalid: $projectRoot; missing: $($missing -join ', ')"
}

$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..\..'))
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'PROJECT_ROOT.txt'))) {
    $workspaceRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot '..\TrayLeaf-output'))
}

$result = [ordered]@{
    ProjectRoot = $projectRoot
    SourceRoot = Join-Path $projectRoot 'src'
    TestsRoot = Join-Path $projectRoot 'tests'
    BuildScript = Join-Path $projectRoot 'build.ps1'
    ReleaseScript = Join-Path $projectRoot 'build-release.ps1'
    WorkspaceRoot = $workspaceRoot
    SoftwareRoot = Join-Path $workspaceRoot 'TrayLeaf'
    ExportRoot = Join-Path $workspaceRoot 'source\releases'
    WorkRoot = Join-Path $workspaceRoot 'work'
}

if ($Json) {
    $result | ConvertTo-Json -Compress
} else {
    Write-Output $projectRoot
}
