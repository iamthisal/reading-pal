Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
function Read-Properties([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { throw "Missing properties: $Path" }
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $Path) {
        $line = $line.Trim()
        if (!$line -or $line.StartsWith('#')) { continue }
        $pair = $line.Split('=', 2)
        if ($pair.Count -ne 2) { throw "Invalid property line in $Path" }
        $values[$pair[0].Trim()] = $pair[1].Trim()
    }
    return $values
}
function Find-JMeter([string]$Requested) {
    $candidates = @($Requested, $env:JMETER_HOME)
    foreach ($candidate in $candidates) {
        if (!$candidate) { continue }
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return (Resolve-Path -LiteralPath $candidate).Path }
        $executable = if ($env:OS -eq 'Windows_NT') { 'bin/jmeter.bat' } else { 'bin/jmeter' }
        $binary = Join-Path $candidate $executable
        if (Test-Path -LiteralPath $binary) { return $binary }
    }
    $commandName = if ($env:OS -eq 'Windows_NT') { 'jmeter.bat' } else { 'jmeter' }
    $cmd = Get-Command $commandName -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $known = 'D:\Apps\apache-jmeter-5.6.3\apache-jmeter-5.6.3\bin\jmeter.bat'
    if ($env:OS -eq 'Windows_NT' -and (Test-Path -LiteralPath $known)) { return $known }
    throw 'JMeter not found. Supply -JMeterPath with the full path to bin/jmeter (Linux) or bin/jmeter.bat (Windows).'
}

function Assert-Local([hashtable]$Config) {
    if ($Config['protocol'] -ne 'http') { throw 'Local suite expects HTTP local launch profiles.' }
    foreach($s in @('user','inventory','lending','notification')) {
        if ($Config["$s.host"] -notin @('localhost','127.0.0.1','::1')) { throw 'Remote hosts are disabled in this local stage.' }
    }
}
function Get-BaseUrl([hashtable]$Config,[string]$Service) {
    return "$($Config['protocol'])://$($Config["$Service.host"]):$($Config["$Service.port"])"
}
function Invoke-LocalApi([hashtable]$Config,[string]$Service,[string]$Method,[string]$Path,$Body=$null,[string]$Token='') {
    $headers=@{}
    if($Token){$headers.Authorization="Bearer $Token"}

    $timeoutSeconds = 30
    if ($Config.ContainsKey('api.timeout.seconds') -and $Config['api.timeout.seconds']) {
        $timeoutSeconds = [int]$Config['api.timeout.seconds']
    }   
    $requestArgs=@{Uri=((Get-BaseUrl $Config $Service)+$Path);Method=$Method;Headers=$headers;TimeoutSec=$timeoutSeconds;ErrorAction='Stop'}
    #$requestArgs=@{Uri=((Get-BaseUrl $Config $Service)+$Path);Method=$Method;Headers=$headers;TimeoutSec=30;ErrorAction='Stop'}
    if($null -ne $Body){$requestArgs.Body=($Body | ConvertTo-Json -Depth 8 -Compress);$requestArgs.ContentType='application/json'}
    try { $result = Invoke-RestMethod @requestArgs; return $result }
    catch { throw "$Service $Method $Path failed. Check service logs; request bodies and credentials are intentionally not printed." }
}



function Assert-Environment([hashtable]$Config,[string]$Environment) {
 if($Environment -eq "local"){ Assert-Local $Config; return }
 if($Config["protocol"] -ne "https"){throw "Azure tests require HTTPS."}
 $allowed=@{
  user='reading-pal-userservice-bwh5bfc0agcvguf6.uaenorth-01.azurewebsites.net'
  inventory='reading-pal-inventoryservice-dnamc3bufybsa8ah.uaenorth-01.azurewebsites.net'
  lending='reading-pal-lendingservice-a6dybkatfsahfkha.uaenorth-01.azurewebsites.net'
  notification='reading-pal-notificationservice-b4gmcqhxgvhxfebu.uaenorth-01.azurewebsites.net'
 }
 foreach($service in $allowed.Keys){if($Config["$service.host"] -ne $allowed[$service] -or $Config["$service.port"] -ne "443"){throw "Azure target must match the documented Reading Pal host and port."}}
}
