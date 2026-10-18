param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ApplicationCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

# Never seed accounts or reset volumes on a developer's existing stack.
if ($env:GITHUB_ACTIONS -ne 'true' -or !$IsLinux) {
    throw 'The load CI adapter requires a fresh Linux GitHub Actions runner. Use scripts/run-parallel-local.ps1 for local runs.'
}
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
$kafkaComposeFile = Join-Path $repositoryRoot 'infrastructure/kafka/docker-compose.yml'
$privateConfig = Join-Path $PSScriptRoot 'config/local.private.json'
$accountsFile = Join-Path $PSScriptRoot 'data/local/accounts.private.json'
$stackStarted = $false
$kafkaStarted = $false

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}

function Wait-Health([string]$BaseUrl) {
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            if ((Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 5).status -eq 'healthy') { return }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Service readiness timed out: $BaseUrl"
}

foreach ($command in @('dotnet', 'docker', 'java', 'tar')) {
    if (!(Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command not found: $command" }
}
foreach ($privateFile in @((Join-Path $repositoryRoot '.env'), $privateConfig, $accountsFile)) {
    if (Test-Path -LiteralPath $privateFile) { throw 'Refusing to overwrite existing configuration or fixtures. Use a fresh CI checkout.' }
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
@{
    applicationCommit = $ApplicationCommit
    jmeterVersion = '5.6.3'
    customers = 20
    fixtureAccounts = 33
    trafficSeconds = 60
    environment = 'Disposable GitHub runner; generator and services share this machine'
    startedUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'ci-manifest.json')

Push-Location $repositoryRoot
try {
    # Pin the existing suite's JMeter version and verify the official archive checksum.
    $toolsDirectory = Join-Path $repositoryRoot '.ase-results/tools'
    New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null
    $archive = Join-Path $toolsDirectory 'apache-jmeter-5.6.3.tgz'
    $downloadBase = 'https://archive.apache.org/dist/jmeter/binaries/apache-jmeter-5.6.3.tgz'
    Invoke-WebRequest -Uri $downloadBase -OutFile $archive -TimeoutSec 180
    $checksumResponse = Invoke-WebRequest -Uri "$downloadBase.sha512" -TimeoutSec 60
    $checksumText = if ($checksumResponse.Content -is [byte[]]) {
        [System.Text.Encoding]::ASCII.GetString($checksumResponse.Content)
    } else { [string]$checksumResponse.Content }
    $expectedHash = [regex]::Match($checksumText, '(?i)\b[a-f0-9]{128}\b').Value
    if (!$expectedHash -or (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $expectedHash) {
        throw 'JMeter archive SHA-512 verification failed.'
    }
    Invoke-Checked 'tar' @('-xzf', $archive, '-C', $toolsDirectory)
    $jmeter = Join-Path $toolsDirectory 'apache-jmeter-5.6.3/bin/jmeter'
    if (!(Test-Path -LiteralPath $jmeter)) { throw 'JMeter executable was not extracted.' }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot '.env.example') -Destination (Join-Path $repositoryRoot '.env')
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:USER_SERVICE_PORT = '5000'
    $env:MYSQL_PORT = '3306'
    $kafkaStarted = $true
    Invoke-Checked 'docker' @('compose', '-f', $kafkaComposeFile, 'up', '-d', '--wait', '--wait-timeout', '240', 'kafka')
    Invoke-Checked 'docker' @('compose', '-f', $kafkaComposeFile, 'run', '--rm', 'kafka-init')
    $stackStarted = $true
    Invoke-Checked 'docker' @(
        'compose', '-p', 'ase-load', '-f', $composeFile, 'up', '-d', '--build', 'mysql',
        'user-service', 'inventory-service', 'lending-service', 'notification-service'
    )
    foreach ($baseUrl in @('http://localhost:5000', 'http://localhost:5001', 'http://localhost:5148', 'http://localhost:5003')) {
        Wait-Health $baseUrl
    }

    # Credentials are only for the disposable Development stack and are never copied to artifacts.
    @{ adminEmail = 'admin@library.com'; adminPassword = 'adminpassword' } |
        ConvertTo-Json | Set-Content -LiteralPath $privateConfig
    & (Join-Path $PSScriptRoot 'scripts/prepare-data.ps1') -Users 33 -SeedNotifications

    # Seeding queues Kafka events; readiness must establish delivery rather than sleeping a fixed time.
    $fixture = Get-Content -LiteralPath $accountsFile -Raw | ConvertFrom-Json
    $deliveryDeadline = [DateTime]::UtcNow.AddMinutes(3)
    foreach ($account in $fixture.accounts) {
        $loginBody = @{ email = $account.userEmail; password = $account.userPassword } | ConvertTo-Json
        $login = Invoke-RestMethod -Method Post -Uri 'http://localhost:5000/api/auth/login' -Body $loginBody -ContentType 'application/json'
        $headers = @{ Authorization = "Bearer $($login.token)" }
        $delivered = $false
        while ([DateTime]::UtcNow -lt $deliveryDeadline) {
            try {
                $unread = Invoke-RestMethod -Uri 'http://localhost:5003/api/notifications/unread-count' -Headers $headers -TimeoutSec 5
                if ($unread.count -gt 0) { $delivered = $true; break }
            }
            catch { }
            Start-Sleep -Seconds 2
        }
        if (!$delivered) { throw 'Seeded notifications were not delivered before the load run.' }
    }
    & (Join-Path $PSScriptRoot 'scripts/run-parallel-local.ps1') -AllowWrites -JMeterPath $jmeter -ApplicationCommit $ApplicationCommit -Customers 20 -DurationSeconds 60
}
finally {
    # Copy only generated reports, never the private fixture/configuration directories.
    $generatedResults = Join-Path $PSScriptRoot 'results/local'
    if (Test-Path -LiteralPath $generatedResults) {
        foreach ($batch in Get-ChildItem -LiteralPath $generatedResults -Directory -Filter 'parallel-*') {
            Copy-Item -LiteralPath $batch.FullName -Destination $ResultsDirectory -Recurse
        }
    }
    if ($stackStarted) {
        & docker compose -p ase-load -f $composeFile logs --no-color --tail 150 2>&1 |
            Out-File -LiteralPath (Join-Path $ResultsDirectory 'services.log') -Encoding utf8
        & docker compose -p ase-load -f $composeFile down --volumes --remove-orphans
    }
    if ($kafkaStarted) {
        & docker compose -f $kafkaComposeFile logs --no-color --tail 100 2>&1 |
            Out-File -LiteralPath (Join-Path $ResultsDirectory 'kafka.log') -Encoding utf8
        & docker compose -f $kafkaComposeFile down --volumes --remove-orphans
    }
    Pop-Location
}
