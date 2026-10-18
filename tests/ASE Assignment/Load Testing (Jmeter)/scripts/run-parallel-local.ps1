param([switch]$AllowWrites,[switch]$Preview,[string]$JMeterPath,[string]$ApplicationCommit='UNVERIFIED',[ValidateRange(20,100)][int]$Customers=20,[ValidateRange(30,3600)][int]$DurationSeconds=60)
. "$PSScriptRoot/common.ps1"
$root=Split-Path $PSScriptRoot -Parent
$definition=Get-Content (Join-Path $root 'config/scenarios.json') -Raw|ConvertFrom-Json
$definition.requiredAccounts=$Customers+13
foreach($job in $definition.groups){if($job.group -eq 'customer-pool'){$job.users=$Customers}}
if($Preview){$definition.groups|Format-Table plan,scenario,users,setup -AutoSize;Write-Host "Required distinct fixture accounts: $($definition.requiredAccounts)";return}
if(!$AllowWrites){throw 'This plan runs existing business workflows. Pass -AllowWrites for the intended local test environment.'}
$config=Read-Properties (Join-Path $root 'config/local.properties');Assert-Local $config
$accounts=Join-Path $root 'data/local/accounts.private.json';$secrets=Join-Path $root 'config/local.private.json'
$data=Get-Content $accounts -Raw|ConvertFrom-Json
if(!$data.complete -or @($data.accounts).Count -lt $definition.requiredAccounts){throw "Need $($definition.requiredAccounts) complete fixture accounts. Existing preparation script can create these."}
$rows=@($data.accounts|Select-Object -First $definition.requiredAccounts)
if(@($rows.userEmail|Sort-Object -Unique).Count -ne $definition.requiredAccounts){throw 'Fixture accounts must be distinct.'}
foreach($row in $rows){foreach($field in @('userEmail','userPassword','bookId','genreId','authorId','listKey','searchTerm','genre','authorName')){if(!$row.PSObject.Properties[$field] -or !$row.$field){throw "Missing fixture field: $field"}}}
# Readiness validation only: no smoke profile or separate test execution.
if(!(Get-Command java -ErrorAction SilentlyContinue)){throw 'Java must be available before starting JMeter.'}
$credentials=Get-Content $secrets -Raw|ConvertFrom-Json
if(!$credentials.adminEmail -or !$credentials.adminPassword -or $credentials.adminPassword -eq 'REPLACE_ME'){throw 'Configure local admin credentials in the ignored private file.'}
[xml]$planXml=Get-Content (Join-Path $root 'test-plans/all-services.jmx') -Raw
if(@($planXml.SelectNodes('//ThreadGroup[@testname="customer-pool"]')).Count -ne 1){throw 'Expected one shared customer pool in the combined plan.'}
foreach($service in @('user','inventory','lending','notification')){$health=Invoke-LocalApi $config $service GET '/health';if($health.status -ne 'healthy'){throw "$service health failed"}}
$jm=Find-JMeter $JMeterPath
$batch=Join-Path $root ('results/local/parallel-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'));New-Item -ItemType Directory $batch|Out-Null
$plan=Join-Path $root 'test-plans/all-services.jmx';$jtl=Join-Path $batch 'results.jtl'
Write-Host "ONE JMETER RUN | $Customers shared customers + 2 shared admins | 21 scenarios" -ForegroundColor Cyan
Write-Host 'Notification read actions run first to avoid collisions with event-producing workflows.'
Write-Host "Repeated traffic: $DurationSeconds seconds including ramp. Bounded workflows: once, up to 90 seconds."
Write-Host "Results: $batch"
& $jm -n -t $plan -q (Join-Path $root 'config/local.properties') -q (Join-Path $root 'config/load.properties') "-Jcustomer.users=$Customers" "-Jduration.seconds=$DurationSeconds" "-Jaccounts.file=$accounts" "-Jsecrets.file=$secrets" -l $jtl -j (Join-Path $batch 'jmeter.log') '-Jjmeter.save.saveservice.output_format=csv' '-Jjmeter.save.saveservice.print_field_names=true' '-Jjmeter.save.saveservice.timestamp_format=ms' '-Jjmeter.save.saveservice.assertion_results_failure_message=true'
$exitCode=$LASTEXITCODE
if(!(Test-Path $jtl)){throw "JMeter produced no results. Exit: $exitCode. Inspect $batch"}
Write-Host "`nFINAL EVALUATION | all traffic has finished" -ForegroundColor Cyan
$all=@(Import-Csv $jtl);$records=@()
foreach($job in $definition.groups){
 $dir=Join-Path $batch ($job.plan+'-'+$job.scenario);New-Item -ItemType Directory $dir|Out-Null
 $expectedNames=@($job.requests|ForEach-Object label)
 $samples=@($all|Where-Object {$_.threadName.StartsWith($job.group+' ') -and ($_.label -in $expectedNames -or $_.label -like 'SETUP |*')})
 $record=[pscustomobject]@{plan=$job.plan;profile=$job.profile;scenario=$job.scenario;status='Failed';reason='';resultDirectory=$dir}
 $samples|Export-Csv (Join-Path $dir 'results.jtl') -NoTypeInformation
 @{threads=$job.users;rampSeconds=0;warmupSeconds=0;holdSeconds=2147483;applicationCommit=$ApplicationCommit;measurement='Short concurrent run, includes ramp; not separate baseline/stress/soak'}|ConvertTo-Json|Set-Content (Join-Path $dir 'run-manifest.json')
 try{
  if(!$samples.Count){throw 'Scenario recorded no samples.'}
  $missing=@($job.requests|Where-Object {$_.label -notin $samples.label})
  $minimum=if($job.profile -eq 'workflow'){1}else{20}
  $labels=@($job.requests|Where-Object phase -eq LOAD|ForEach-Object label|Sort-Object -Unique)
  & "$PSScriptRoot/check-results.ps1" -ResultsFile (Join-Path $dir 'results.jtl') -OutputFile (Join-Path $dir 'summary.json') -Quiet -MinimumSamples $minimum -ExpectedLabels $labels -RequireZeroFailures:($job.profile -eq 'workflow') *> (Join-Path $dir 'gate.log')
  if($missing.Count){throw 'Some expected setup/request labels did not execute.'}
  if($job.plan -eq 'mixed-user-workload'){
   $participating=@($samples|Where-Object {$_.label -like 'LOAD |*' -and $_.success -eq 'true'}|Select-Object -ExpandProperty threadName -Unique).Count
   if($participating -lt $Customers){throw "Only $participating of $Customers customers recorded successful mixed-workload requests."}
  }
  if($exitCode -ne 0){throw "JMeter execution failed with exit $exitCode"}
  $record.status='Passed'
 }catch{$record.reason=$_.Exception.Message}
 $records+=$record
}
# Exact raw counts: scenario slices intentionally share setup samples and must not be summed.
$http=@($all|Where-Object {$_.URL -match '^https?://'})
$performance=@($http|Where-Object {$_.label -like 'LOAD |*' -and ($_.threadName -like 'customer-pool *' -or $_.threadName -like 'admin-pool *')})
$start=[long](($http|ForEach-Object{[long]$_.timeStamp}|Measure-Object -Minimum).Minimum)
$end=[long](($http|ForEach-Object{[long]$_.timeStamp+[long]$_.elapsed}|Measure-Object -Maximum).Maximum)
$seconds=($end-$start)/1000.0
$overall=[ordered]@{customers=$Customers;adminThreads=2;workflowThreads=9;setupThreads=2;configuredTrafficSeconds=$DurationSeconds;observedHttpSeconds=$seconds;httpRequests=$http.Count;internalSamples=$all.Count-$http.Count;httpFailures=@($http|Where-Object success -ne 'true').Count;httpRequestsPerSecond=$(if($seconds -gt 0){[Math]::Round($http.Count/$seconds,2)}else{0});applicationCommit=$ApplicationCommit;measurement='Local, includes ramp and setup; not maximum capacity'}
$overall|ConvertTo-Json|Set-Content (Join-Path $batch 'run-overview.json') -Encoding UTF8
Write-Host "`nACTUAL RUN TOTALS (counted once from raw results)" -ForegroundColor Cyan
[pscustomobject]$overall|Format-List|Out-Host
$ranked=@($performance|Group-Object label|ForEach-Object{
 $times=@($_.Group|ForEach-Object{[double]$_.elapsed}|Sort-Object)
 [pscustomobject]@{request=$_.Name;samples=$times.Count;p95Ms=$times[[Math]::Ceiling(.95*$times.Count)-1];p99Ms=$times[[Math]::Ceiling(.99*$times.Count)-1]}
}|Sort-Object p95Ms -Descending)
$ranked|Export-Csv (Join-Path $batch 'endpoint-performance.csv') -NoTypeInformation
Write-Host 'SLOWEST REPEATED ENDPOINTS BY P95 (investigation candidates, not proven bottlenecks)' -ForegroundColor Cyan
$ranked|Select-Object -First 5|Format-Table -AutoSize|Out-Host
$records|ConvertTo-Json -Depth 6|Set-Content (Join-Path $batch 'suite-runs.json') -Encoding UTF8
& "$PSScriptRoot/build-suite-report.ps1" -SuiteDirectory $batch
# The shared report normally describes separate profiles; replace that text for this concurrent run.
$html=Get-Content (Join-Path $batch 'index.html') -Raw
$html=$html.Replace('Performance summaries exclude setup and ramp/warm-up.','This short concurrent run excludes setup samples but includes ramp-up; Repeated load checks require at least 20 samples per expected label; one-off workflow checks require one. Small samples do not establish reliable tail latency.')
$html=$html.Replace('Local service performance and workflow verification','Short concurrent load and workflow verification (not a baseline/stress/soak campaign)')
$html=$html.Replace('<main>', "<main><aside><strong>Shared pools:</strong> $Customers customers and 2 admin browsing threads. Scenario user counts refer to these same pools; do not add them together. Nine independent one-off workflows also run. Two notification setup threads run first.</aside>")
$html=$html.Replace('</nav>','<a href="html-report/index.html">Full JMeter dashboard</a></nav>')
$html|Set-Content (Join-Path $batch 'index.html') -Encoding UTF8
& $jm -g $jtl -o (Join-Path $batch 'html-report') -j (Join-Path $batch 'report.log')
if($LASTEXITCODE -ne 0){throw "Dashboard generation failed. Metrics remain in $batch"}
Write-Host "Report: $(Join-Path $batch 'index.html')" -ForegroundColor Cyan
if(@($records|Where-Object status -ne Passed).Count){throw 'Some scenarios failed or coverage was incomplete; inspect the report.'}

