param(
    [Parameter(Mandatory)][string]$ResultsFile,
    [Parameter(Mandatory)][string]$OutputFile,
    [string]$ThresholdsFile = (Join-Path (Split-Path $PSScriptRoot -Parent) 'config/thresholds.properties'),
    [long]$MeasurementStartMs = 0,
    [long]$MeasurementEndMs = [long]::MaxValue,
    [int]$MinimumSamples = 0,
    [string[]]$ExpectedLabels = @(),
    [switch]$RequireZeroFailures,
    [switch]$Quiet
)
. "$PSScriptRoot/common.ps1"
$thresholds = Read-Properties $ThresholdsFile
if ($MinimumSamples -le 0) { $MinimumSamples = [int]$thresholds['minimum.samples.per.label'] }
if (!(Test-Path -LiteralPath $ResultsFile)) { throw 'JMeter did not produce a result file.' }
$all = @(Import-Csv -LiteralPath $ResultsFile)
$setupFailures = @($all | Where-Object { $_.label -notlike 'LOAD |*' -and $_.success -ne 'true' }).Count
$samples = @($all | Where-Object { $_.label -like 'LOAD |*' -and [long]$_.timeStamp -ge $MeasurementStartMs -and [long]$_.timeStamp -lt $MeasurementEndMs })
$contractFailures = @($samples | Where-Object { $_.PSObject.Properties['failureMessage'] -and $_.failureMessage }).Count
$passed = $samples.Count -gt 0 -and $setupFailures -eq 0 -and $contractFailures -eq 0
$stats = @()
foreach ($label in @(@($samples | ForEach-Object { $_.label }) + $ExpectedLabels | Sort-Object -Unique)) {
    $items = @($samples | Where-Object label -eq $label)
    $count = $items.Count
    $labelContractFailures = @($items | Where-Object { $_.PSObject.Properties['failureMessage'] -and $_.failureMessage }).Count
    $failed = @($items | Where-Object success -ne 'true').Count
    $times = @($items | ForEach-Object { [double]$_.elapsed } | Sort-Object)
    $p95 = if ($count) { $times[[Math]::Ceiling(0.95*$count)-1] } else { $null }
    $rate = if ($count) { $failed / $count } else { 1.0 }
    $limit = [double]$thresholds['p95.default.ms']
    if ($label -like '*| lending |*') { $limit = [double]$thresholds['p95.lending.ms'] }
    elseif ($label -like '*| user |*') { $limit = [double]$thresholds['p95.user.ms'] }
    $ok = $labelContractFailures -eq 0 -and $count -ge $MinimumSamples -and $rate -le $(if($RequireZeroFailures){0}else{[double]$thresholds['error.rate.max']}) -and $null -ne $p95 -and $p95 -le $limit
    if (!$ok) { $passed = $false }
    $stats += [pscustomobject]@{label=$label;samples=$count;failures=$failed;errorRate=$rate;p95Ms=$p95;p95LimitMs=$limit;passed=$ok}
}
$duration = if ($MeasurementEndMs -lt [long]::MaxValue) { ($MeasurementEndMs-$MeasurementStartMs)/1000.0 } elseif ($samples.Count) {
    (($samples | ForEach-Object { [long]$_.timeStamp + [long]$_.elapsed } | Measure-Object -Maximum).Maximum - ($samples | ForEach-Object { [long]$_.timeStamp } | Measure-Object -Minimum).Minimum)/1000.0
} else { 0 }
$summary = [pscustomobject]@{passed=$passed;setupFailures=$setupFailures;contractFailures=$contractFailures;measuredSamples=$samples.Count;measurementSeconds=$duration;requestsPerSecond=$(if($duration -gt 0){$samples.Count/$duration}else{0});minimumSamplesPerLabel=$MinimumSamples;endpoints=$stats}
$summary | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputFile -Encoding utf8
if (!$Quiet) { $stats | Format-Table label,samples,failures,p95Ms,passed -AutoSize | Out-Host }
if (!$passed) { throw "Performance/contract gate failed or insufficient samples. See $OutputFile" }
if (!$Quiet) { Write-Host 'Result gate passed.' }

