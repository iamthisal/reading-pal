param(
    [Parameter(Mandatory)]
    [ValidateSet('Discover', 'Unit', 'Mutation', 'Ui', 'Load')]
    [string]$Suite,
    [string]$ResultsDirectory = $env:ASE_RESULTS_DIRECTORY
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$assignmentRoot = Split-Path $PSScriptRoot -Parent
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $assignmentRoot '../..'))
if (!$ResultsDirectory) {
    $ResultsDirectory = Join-Path $repositoryRoot '.ase-results'
}
$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
$entryPoints = [ordered]@{
    Mutation = Join-Path $assignmentRoot 'Mutation Testing (Stryker)/run-ci.ps1'
    Ui = Join-Path $assignmentRoot 'E2E Testing (Selenium)/run-ci.ps1'
    Load = Join-Path $assignmentRoot 'Load Testing (Jmeter)/run-ci.ps1'
}

if ($Suite -eq 'Discover') {
    $lines = @('# ASE suite connections', '', '| Suite | CI connection |', '| --- | --- |', '| Unit | Four projects connected |')
    foreach ($name in $entryPoints.Keys) {
        $connected = Test-Path -LiteralPath $entryPoints[$name] -PathType Leaf
        $state = if ($connected) { 'Entry point found; execution must still pass' } else { 'PENDING: run-ci.ps1 missing' }
        $lines += "| $name | $state |"
        if ($env:GITHUB_OUTPUT) {
            "$($name.ToLowerInvariant())=$($connected.ToString().ToLowerInvariant())" |
                Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding utf8
        }
    }
    $lines | Write-Output
    if ($env:GITHUB_STEP_SUMMARY) {
        $lines | Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Encoding utf8
    }
    return
}

$suiteResults = Join-Path $ResultsDirectory $Suite.ToLowerInvariant()
New-Item -ItemType Directory -Path $suiteResults -Force | Out-Null
Push-Location $repositoryRoot
try {
    if ($Suite -eq 'Unit') {
        $failedProjects = @()
        foreach ($service in @('User', 'Inventory', 'Lending', 'Notification')) {
            $project = Join-Path $assignmentRoot "Unit Testing (xUnit-Moq)/$($service)Service.UnitTests/$($service)Service.UnitTests.csproj"
            if (!(Test-Path -LiteralPath $project -PathType Leaf)) {
                throw "Required unit test project is missing: $project"
            }
            $projectResults = Join-Path $suiteResults $service
            $testArguments = @(
                'test', $project, '--configuration', 'Release', '--verbosity', 'minimal',
                '--logger', "trx;LogFileName=$($service)Service.UnitTests.trx",
                '--results-directory', $projectResults
            )
            & dotnet @testArguments
            if ($LASTEXITCODE -ne 0) { $failedProjects += $service }
        }
        if ($failedProjects.Count -gt 0) {
            throw "Unit test projects failed: $($failedProjects -join ', ')"
        }
        return
    }

    $entryPoint = $entryPoints[$Suite]
    if (!(Test-Path -LiteralPath $entryPoint -PathType Leaf)) {
        throw "$Suite is not connected. Add $entryPoint using the contract in tests/ASE Assignment/README.md."
    }
    $applicationCommit = if ($env:GITHUB_SHA) { $env:GITHUB_SHA } else {
        $revision = & git rev-parse HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the application commit.' }
        $revision
    }
    # A fresh PowerShell process makes the teammate script's exit code unambiguous.
    & pwsh -NoProfile -File $entryPoint -ResultsDirectory $suiteResults -ApplicationCommit $applicationCommit
    if ($LASTEXITCODE -ne 0) { throw "$Suite entry point failed with exit code $LASTEXITCODE." }
    if (!(Get-ChildItem -LiteralPath $suiteResults -Recurse -File | Select-Object -First 1)) {
        throw "$Suite returned success without saving a result file to $suiteResults."
    }
}
finally {
    Pop-Location
}
