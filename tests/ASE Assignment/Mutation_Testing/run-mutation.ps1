# Runs Stryker.NET mutation testing for each microservice's unit tests, one service at a time.
# Each service folder holds a stryker-config.json whose relative paths resolve from that folder,
# so Stryker is always started from inside it.
#
# Usage (from anywhere):
#   .\run-mutation.ps1                                  # all four services
#   .\run-mutation.ps1 -Services UserService,LendingService
#
# Output:
#   <Service>\StrykerOutput\<timestamp>\   raw Stryker run (gitignored)
#   reports\<Service>.html                 latest HTML report per service
#   reports\<Service>.summary.json         latest counts and score per service
#   reports\summary.md                     score table for all services run so far

param(
    [ValidateSet("UserService", "InventoryService", "LendingService", "NotificationService")]
    [string[]]$Services = @("UserService", "InventoryService", "LendingService", "NotificationService")
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$reportsDir = Join-Path $root "reports"
New-Item -ItemType Directory -Force $reportsDir | Out-Null

$failed = @()

foreach ($service in $Services) {
    $serviceDir = Join-Path $root $service
    Write-Host "`n=== $service ===" -ForegroundColor Cyan

    $outputDir = Join-Path $serviceDir "StrykerOutput"
    $before = @()
    if (Test-Path $outputDir) { $before = @(Get-ChildItem $outputDir -Directory | ForEach-Object Name) }

    Push-Location $serviceDir
    try {
        dotnet stryker
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    $run = $null
    if (Test-Path $outputDir) {
        $run = Get-ChildItem $outputDir -Directory | Where-Object { $before -notcontains $_.Name } |
            Sort-Object Name | Select-Object -Last 1
    }
    $jsonReport = if ($run) { Join-Path $run.FullName "reports/mutation-report.json" } else { $null }
    if ($exitCode -ne 0 -or -not $jsonReport -or -not (Test-Path $jsonReport)) {
        Write-Host "$service failed (exit code $exitCode); no report produced." -ForegroundColor Red
        $failed += $service
        continue
    }

    # Count mutant statuses from the raw text (Windows PowerShell's ConvertFrom-Json struggles with large reports).
    $text = [IO.File]::ReadAllText($jsonReport)
    $counts = @{}
    foreach ($m in [regex]::Matches($text, '"status"\s*:\s*"(\w+)"')) {
        $status = $m.Groups[1].Value
        $counts[$status] = 1 + [int]$counts[$status]
    }
    $killed = [int]$counts["Killed"]; $timeout = [int]$counts["Timeout"]
    $survived = [int]$counts["Survived"]; $noCoverage = [int]$counts["NoCoverage"]
    $valid = $killed + $timeout + $survived + $noCoverage
    # Same formula Stryker reports: detected / (detected + undetected); compile errors and ignored mutants don't count.
    $score = if ($valid -gt 0) { [math]::Round(100 * ($killed + $timeout) / $valid, 2) } else { 0 }

    Copy-Item (Join-Path $run.FullName "reports/mutation-report.html") (Join-Path $reportsDir "$service.html") -Force
    [ordered]@{
        service    = $service
        run        = $run.Name
        killed     = $killed
        timeout    = $timeout
        survived   = $survived
        noCoverage = $noCoverage
        score      = $score
    } | ConvertTo-Json | Set-Content (Join-Path $reportsDir "$service.summary.json") -Encoding utf8

    Write-Host ("{0}: mutation score {1}%" -f $service, $score) -ForegroundColor Green
}

# Rebuild the summary table from every service's latest result, including ones not run this time.
$rows = Get-ChildItem $reportsDir -Filter "*.summary.json" | Sort-Object Name |
    ForEach-Object { Get-Content $_.FullName -Raw | ConvertFrom-Json }
$lines = @(
    "# Mutation testing summary",
    "",
    "Stryker.NET mutation testing of each microservice's unit tests. Score = (killed + timeout) / (killed + timeout + survived + no coverage).",
    "",
    "| Service | Run | Killed | Timeout | Survived | No coverage | Mutation score |",
    "|---|---|---|---|---|---|---|"
)
foreach ($r in $rows) {
    $lines += "| $($r.service) | $($r.run) | $($r.killed) | $($r.timeout) | $($r.survived) | $($r.noCoverage) | **$($r.score)%** |"
}
$lines | Set-Content (Join-Path $reportsDir "summary.md") -Encoding utf8

Write-Host "`nSummary written to $(Join-Path $reportsDir 'summary.md')" -ForegroundColor Cyan
$rows | Format-Table service, killed, timeout, survived, noCoverage, score -AutoSize

if ($failed.Count -gt 0) {
    Write-Host "Failed: $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
