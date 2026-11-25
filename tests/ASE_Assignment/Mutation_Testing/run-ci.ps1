# Entry point called by scripts/Invoke-AseTests.ps1 for the shared ASE pipeline.
param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ApplicationCommit
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "run-mutation.ps1") -ReportsDirectory $ResultsDirectory -Commit $ApplicationCommit
$exitCode = $LASTEXITCODE

$summary = Join-Path $ResultsDirectory "summary.md"
if ($env:GITHUB_STEP_SUMMARY -and (Test-Path $summary)) {
    Get-Content $summary | Add-Content $env:GITHUB_STEP_SUMMARY -Encoding utf8
}

exit $exitCode
