param([Parameter(Mandatory)][string]$SuiteDirectory,[switch]$LatestOnly)
. "$PSScriptRoot/common.ps1"
function Escape($v){[System.Net.WebUtility]::HtmlEncode([string]$v)}
function Percentile($values,$fraction){if($values.Count){$values[[Math]::Ceiling($values.Count*$fraction)-1]}else{$null}}
$records=@(Get-Content (Join-Path $SuiteDirectory 'suite-runs.json') -Raw|ConvertFrom-Json|ForEach-Object { $_ })
$cards=@();$metrics=@()
foreach($record in $records){
 $summary=$null;$manifest=$null;$samples=@();$rows='';$links='';$m=$null
 if($record.resultDirectory){
  $dir=$record.resultDirectory
  if(Test-Path (Join-Path $dir 'run-manifest.json')){$manifest=Get-Content (Join-Path $dir 'run-manifest.json') -Raw|ConvertFrom-Json}
  if(Test-Path (Join-Path $dir 'summary.json')){$summary=Get-Content (Join-Path $dir 'summary.json') -Raw|ConvertFrom-Json}
  if($summary -and $manifest -and (Test-Path (Join-Path $dir 'results.jtl'))){
   $all=@(Import-Csv (Join-Path $dir 'results.jtl'))
   $first=[long](($all|ForEach-Object{[long]$_.timeStamp}|Measure-Object -Minimum).Minimum)
   $start=$first+1000*($manifest.rampSeconds+$manifest.warmupSeconds)
   if($record.profile -in @('smoke','workflow')){$start=$first}
   $end=$first+1000*($manifest.rampSeconds+$manifest.warmupSeconds+$manifest.holdSeconds)
   if($record.profile -eq 'workflow'){$end=[long]::MaxValue}
   $samples=@($all|Where-Object {$_.label -like 'LOAD |*' -and [long]$_.timeStamp -ge $start -and [long]$_.timeStamp -lt $end})
   $times=@($samples|ForEach-Object{[double]$_.elapsed}|Sort-Object)
   $failures=@($samples|Where-Object success -ne 'true').Count
   $avg=if($times.Count){[Math]::Round(($times|Measure-Object -Average).Average,2)}else{$null}
   $errorPct=if($samples.Count){[Math]::Round(100*$failures/$samples.Count,2)}else{$null}
   $m=[pscustomobject]@{service=$record.plan;type=$record.profile;scenario=$record.scenario;status=$record.status;users=$manifest.threads;seconds=[Math]::Round($summary.measurementSeconds,2);requests=$samples.Count;averageMs=$avg;p95Ms=(Percentile $times .95);p99Ms=(Percentile $times .99);requestsPerSecond=[Math]::Round($summary.requestsPerSecond,2);errorsPercent=$errorPct;setupFailures=$summary.setupFailures;assertionFailures=$summary.contractFailures}
   $metrics+=$m
   foreach($ep in $summary.endpoints){
    $rows+="<tr><td>$(Escape $ep.label)</td><td>$($ep.samples)</td><td>$($ep.failures)</td><td>$($ep.p95Ms)</td><td>$($ep.p95LimitMs)</td><td>$($ep.passed)</td></tr>"
   }
  }
  $folder=[Uri]::EscapeDataString((Split-Path $dir -Leaf))
  if(Test-Path (Join-Path $dir 'html-report/index.html')){$links="<a href='../$folder/html-report/index.html'>Detailed JMeter report</a>"}
 }
 $body=if($m){"<div class='stats'><b>$($m.users) users</b><b>$($m.requests) requests</b><b>$($m.seconds)s measured</b><b>$($m.requestsPerSecond) req/s</b><b>$($m.errorsPercent)% errors</b></div><p>Average: $($m.averageMs) ms | p95: $($m.p95Ms) ms | p99: $($m.p99Ms) ms | Setup failures: $($m.setupFailures) | Assertion failures: $($m.assertionFailures)</p>"}else{'<p>Measurements unavailable or test not executed. No performance pass is inferred.</p>'}
 $cards += [pscustomobject]@{plan=$record.plan;profile=$record.profile;html="<article><h3>$(Escape $record.scenario) <span class='$($record.status)'>$($record.status)</span></h3><p>$(Escape $record.reason)</p>$body<p>$links</p><details><summary>Endpoint checks and thresholds</summary><table><tr><th>Request</th><th>Samples</th><th>Failures</th><th>p95 ms</th><th>Limit ms</th><th>Passed</th></tr>$rows</table></details></article>"}
 if(!$LatestOnly -or $record -eq $records[-1]){
  $color=if($record.status -eq 'Passed'){'Green'}elseif($record.status -eq 'Failed'){'Red'}else{'Yellow'}
  Write-Host "$($record.plan) > $($record.profile) > $($record.scenario): $($record.status)" -ForegroundColor $color
  $explained=$false
  if($record.status -eq 'Failed' -and $summary){
   foreach($endpoint in @($summary.endpoints|Where-Object { !$_.passed })){
    Write-Host "  [FAIL] $($endpoint.label)" -ForegroundColor Red
    if($null -ne $endpoint.p95Ms -and $endpoint.p95Ms -gt $endpoint.p95LimitMs){
     Write-Host '    Response time exceeded the allowed limit.' -ForegroundColor Red
     Write-Host "    Actual p95: $($endpoint.p95Ms) ms | Allowed p95: $($endpoint.p95LimitMs) ms" -ForegroundColor Yellow
     $explained=$true
    }
    if($endpoint.samples -lt $summary.minimumSamplesPerLabel){
     Write-Host "    Insufficient samples: $($endpoint.samples) recorded; at least $($summary.minimumSamplesPerLabel) required." -ForegroundColor Red
     $explained=$true
    }
    if($endpoint.failures -gt 0){
     Write-Host "    Failed requests: $($endpoint.failures)/$($endpoint.samples) ($([Math]::Round(100*$endpoint.errorRate,2))%)." -ForegroundColor Red
     $explained=$true
    }else{Write-Host '    Failed requests: 0 (requests succeeded; another check failed).' -ForegroundColor Yellow}
   }
   if($summary.setupFailures -gt 0){Write-Host "  [FAIL] $($summary.setupFailures) setup checks failed (such as login or fixture setup)." -ForegroundColor Red;$explained=$true}
   if($summary.contractFailures -gt 0){Write-Host "  [FAIL] $($summary.contractFailures) response assertions failed; the returned status or data did not satisfy the test." -ForegroundColor Red;$explained=$true}
   if($record.resultDirectory -and (Test-Path (Join-Path $record.resultDirectory 'results.jtl'))){
    $failedRows=@(Import-Csv (Join-Path $record.resultDirectory 'results.jtl')|Where-Object success -ne 'true')
    foreach($failure in ($failedRows|Group-Object label)){
     $codes=@($failure.Group|ForEach-Object{
      if($_.responseCode -match '^\d{3}$'){"HTTP $($_.responseCode)"}
      elseif($_.responseCode -match '([A-Za-z][A-Za-z0-9]*Exception)'){$Matches[1]}
      else{'script/transport error'}
     }|Sort-Object -Unique)
     Write-Host "    $($failure.Name): $($codes -join ', ')" -ForegroundColor Red
    }
   }
  }
  if($record.reason -and !$explained){Write-Host "  [FAIL] $($record.reason)" -ForegroundColor $color}
  if($m){$m|Format-List users,seconds,requests,averageMs,p95Ms,p99Ms,requestsPerSecond,errorsPercent,setupFailures,assertionFailures|Out-Host;$summary.endpoints|Format-Table label,samples,failures,p95Ms,p95LimitMs,passed -AutoSize|Out-Host}
 }
}
$sections=''
foreach($plan in @('user-service','inventory-service','lending-service','notification-service','mixed-user-workload')){
 $sections+="<section id='$plan'><h2>$plan</h2>"
 foreach($profile in @('smoke','workflow','baseline','load','stress','recovery','soak')){
  $group=@($cards|Where-Object {$_.plan -eq $plan -and $_.profile -eq $profile})
  if($group.Count){$sections+="<h3 class='type'>$($profile.ToUpper())</h3>"+($group.html -join '')}
 }
 $base=@($metrics|Where-Object {$_.service -eq $plan -and $_.type -eq 'baseline' -and $_.scenario -eq 'reads'})
 $load=@($metrics|Where-Object {$_.service -eq $plan -and $_.type -eq 'load' -and $_.scenario -eq 'reads'})
 if($base.Count -and $load.Count){$sections+="<p class='comparison'>Read workload comparison: baseline p95 $($base[0].p95Ms) ms / $($base[0].requestsPerSecond) req/s; load p95 $($load[0].p95Ms) ms / $($load[0].requestsPerSecond) req/s. Compare errors and endpoint gates too; throughput alone is not a pass.</p>"}
 $sections+='</section>'
}
$counts=($records|Group-Object status|ForEach-Object{"$($_.Name): $($_.Count)"}) -join ' | '
$html=@"
<!doctype html><html lang='en'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>Reading Pal | Local Performance Report</title><style>
body{font:16px system-ui,sans-serif;background:#f3f6fc;color:#17263e;margin:0}header{background:#132c50;color:white;padding:40px}main{max-width:1200px;margin:auto;padding:24px}nav a{display:inline-block;margin:8px;color:#1264ae}section,aside{background:white;padding:24px;margin:24px 0;border-radius:14px;box-shadow:0 4px 18px #142c5010}article{border:1px solid #dbe3ef;border-radius:10px;padding:20px;margin:16px 0}.type{border-left:5px solid #326ec6;padding:10px;background:#edf3ff}.stats{display:flex;gap:18px;flex-wrap:wrap}.Passed{color:#087846}.Failed{color:#bd263a}.Skipped,.Pending{color:#8a5900}span{float:right}table{border-collapse:collapse;width:100%;font-size:13px}th,td{text-align:left;padding:10px;border-bottom:1px solid #dde4ef;overflow-wrap:anywhere}details{overflow:auto}summary{cursor:pointer;padding:12px 0}.comparison{background:#edf3ff;padding:16px}a{color:#1264ae}h2{border-bottom:2px solid #e1e9f4;padding-bottom:12px}@media print{details{display:block}header{background:white;color:black}article{break-inside:avoid}}
</style></head><body><header><h1>Reading Pal</h1><p>Local service performance and workflow verification</p><h2>$counts</h2><p>Recorded runs: $($records.Count). Report updated after each run; an interrupted batch may be incomplete.</p></header><main><nav><a href='#user-service'>User</a><a href='#inventory-service'>Inventory</a><a href='#lending-service'>Lending</a><a href='#notification-service'>Notification</a><a href='#mixed-user-workload'>Mixed</a></nav><aside><h2>How to explain these results</h2><p>Smoke checks basic operation. Workflow checks bounded business actions and expected rejections. Baseline measures light traffic; load measures expected test traffic. Stress explores higher traffic, recovery checks behaviour after it, and soak checks sustained operation.</p><p>p95 means 95% of measured requests completed within this time; p99 describes the slower tail. Throughput is completed requests per second, not concurrent users. Errors include unexpected failures; deliberately expected 4xx checks can pass. Percentiles use nearest rank and are calculated from raw samples, not averaged endpoint percentiles.</p><p>Performance summaries exclude setup and ramp/warm-up. Detailed JMeter dashboards include all phases. Workflow runs use one iteration per user. Aggregate percentiles mix endpoints; endpoint gates below determine acceptance. These are local test observations, not proof of production capacity. Actual application commit and configuration are recorded in each run manifest.</p><p>Missing or skipped runs are not passes. No browser rendering, fine-date boundary or Kafka delivery-latency coverage is claimed. Monitor CPU, RAM and Kafka externally; stop manually if errors stay above 5% for a minute or the machine becomes unresponsive.</p></aside>$sections</main></body></html>
"@
$html|Set-Content (Join-Path $SuiteDirectory 'index.html') -Encoding UTF8
$metrics|Export-Csv (Join-Path $SuiteDirectory 'metrics.csv') -NoTypeInformation -Encoding UTF8
