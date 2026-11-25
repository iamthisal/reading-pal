# Runs Stryker.NET mutation testing against the ASE unit tests, one microservice at a time.
#
# Usage (from anywhere):
#   .\run-mutation.ps1                                        # all four services
#   .\run-mutation.ps1 -Services UserService,LendingService   # selected services
#   .\run-mutation.ps1 -ReportsDirectory C:\temp\mutation     # write the reports elsewhere
#
# Output in the reports directory:
#   <Service>.html, <Service>.summary.json, summary.md

param(
    [ValidateSet("UserService", "InventoryService", "LendingService", "NotificationService")]
    [string[]]$Services = @("UserService", "InventoryService", "LendingService", "NotificationService"),
    [string]$ReportsDirectory = (Join-Path $PSScriptRoot "reports"),
    [string]$Commit
)

$ErrorActionPreference = "Stop"
$ReportsDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ReportsDirectory)
New-Item -ItemType Directory -Force $ReportsDirectory | Out-Null

# The tool manifest in .config pins the Stryker version.
Push-Location $PSScriptRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}

$failed = @()

foreach ($service in $Services) {
    $serviceDir = Join-Path $PSScriptRoot $service
    $runDir = Join-Path $serviceDir "StrykerOutput/latest"
    Write-Host "`n=== $service ===" -ForegroundColor Cyan

    # Remove the previous result so a failed run can never be reported as a fresh one.
    if (Test-Path $runDir) { Remove-Item $runDir -Recurse -Force }
    foreach ($extension in @("html", "summary.json")) {
        $previous = Join-Path $ReportsDirectory "$service.$extension"
        if (Test-Path $previous) { Remove-Item $previous -Force }
    }

    # Paths in stryker-config.json resolve from the service folder.
    Push-Location $serviceDir
    try {
        dotnet stryker --output $runDir
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        Write-Host "$service failed (Stryker exit code $exitCode)." -ForegroundColor Red
        $failed += $service
    }
    $jsonReport = Join-Path $runDir "reports/mutation-report.json"
    if (-not (Test-Path $jsonReport)) {
        Write-Host "$service produced no report." -ForegroundColor Red
        if ($failed -notcontains $service) { $failed += $service }
        continue
    }

    # Count statuses from the raw text: Windows PowerShell's ConvertFrom-Json struggles with large reports.
    $counts = @{}
    foreach ($match in [regex]::Matches([IO.File]::ReadAllText($jsonReport), '"status"\s*:\s*"(\w+)"')) {
        $status = $match.Groups[1].Value
        $counts[$status] = 1 + [int]$counts[$status]
    }
    $killed = [int]$counts["Killed"]; $timeout = [int]$counts["Timeout"]
    $survived = [int]$counts["Survived"]; $noCoverage = [int]$counts["NoCoverage"]
    $valid = $killed + $timeout + $survived + $noCoverage
    if ($valid -eq 0) {
        Write-Host "$service tested no mutants." -ForegroundColor Red
        if ($failed -notcontains $service) { $failed += $service }
        continue
    }
    # Same formula Stryker uses: compile errors and ignored mutants do not count.
    $score = [math]::Round(100 * ($killed + $timeout) / $valid, 2)

    Copy-Item (Join-Path $runDir "reports/mutation-report.html") (Join-Path $ReportsDirectory "$service.html") -Force
    [ordered]@{
        service    = $service
        run        = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'")
        commit     = $Commit
        killed     = $killed
        timeout    = $timeout
        survived   = $survived
        noCoverage = $noCoverage
        score      = $score
    } | ConvertTo-Json | Set-Content (Join-Path $ReportsDirectory "$service.summary.json") -Encoding ascii

    Write-Host ("{0}: mutation score {1}%" -f $service, $score) -ForegroundColor Green
}

# Rebuild the table from every service's latest result, including ones not run this time.
$rows = @(Get-ChildItem $ReportsDirectory -Filter "*.summary.json" | Sort-Object Name |
    ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json })
$lines = @(
    "# Mutation testing summary",
    "",
    "Stryker.NET mutation testing of the ASE unit tests. Score = (killed + timeout) / (killed + timeout + survived + no coverage).",
    "",
    "| Service | Run | Killed | Timeout | Survived | No coverage | Mutation score |",
    "| --- | --- | --- | --- | --- | --- | --- |"
)
foreach ($row in $rows) {
    $lines += "| $($row.service) | $($row.run) | $($row.killed) | $($row.timeout) | $($row.survived) | $($row.noCoverage) | **$($row.score)%** |"
}
if ($failed.Count -gt 0) { $lines += @("", "Failed: $($failed -join ', ')") }
$lines | Set-Content (Join-Path $ReportsDirectory "summary.md") -Encoding ascii

Write-Host "`nSummary written to $(Join-Path $ReportsDirectory 'summary.md')" -ForegroundColor Cyan

if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
exit 0
