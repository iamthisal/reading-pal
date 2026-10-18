param(
    [Parameter(Mandatory)][string]$ResultsDirectory,
    [Parameter(Mandatory)][string]$ApplicationCommit,
    [switch]$UseExistingStack
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Handle native exit codes explicitly so all four test projects can report their failures.
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$ResultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
$frontendRoot = Join-Path $repositoryRoot 'frontend'
$composeFile = Join-Path $repositoryRoot 'docker-compose.yml'
$kafkaComposeFile = Join-Path $repositoryRoot 'infrastructure/kafka/docker-compose.yml'
$services = @('user-service', 'inventory-service', 'lending-service', 'notification-service')
$projects = @('UserService', 'InventoryService', 'LendingService', 'NotificationService')
$frontendProcess = $null
$stackStarted = $false
$kafkaStarted = $false
$ownsStack = !$UseExistingStack

function Invoke-Checked([string]$Command, [string[]]$Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command failed with exit code $LASTEXITCODE." }
}

function Wait-Endpoint([string]$Url, [int]$Seconds = 180, [switch]$Health) {
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -TimeoutSec 5
            if ($response.StatusCode -eq 200 -and (!$Health -or ($response.Content | ConvertFrom-Json).status -eq 'healthy')) {
                return
            }
        }
        catch { }
        Start-Sleep -Seconds 2
    }
    throw "Readiness timed out: $Url"
}

# Provisioning and cleanup are allowed only on a fresh hosted CI checkout.
# Local runs must opt into reusing an already-started stack; no local data is reset.
if ($ownsStack -and ($env:GITHUB_ACTIONS -ne 'true' -or !$IsLinux)) {
    throw 'Automatic provisioning requires a Linux GitHub Actions runner. For a local test stack, pass -UseExistingStack.'
}
foreach ($command in @('dotnet', 'docker', 'node', 'npm')) {
    if (!(Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command not found: $command" }
}
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
@{
    applicationCommit = $ApplicationCommit
    suite = 'Selenium E2E'
    startedUtc = [DateTime]::UtcNow.ToString('o')
    environment = if ($ownsStack) { 'Disposable GitHub runner' } else { 'Existing local stack' }
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $ResultsDirectory 'run-manifest.json')

Push-Location $repositoryRoot
try {
    # Compile first so a bad test project fails before provisioning infrastructure.
    foreach ($project in $projects) {
        $projectFile = Join-Path $PSScriptRoot "$project.E2ETests/$project.E2ETests.csproj"
        Invoke-Checked 'dotnet' @('build', $projectFile, '--configuration', 'Release', '--verbosity', 'minimal')
    }

    if ($ownsStack) {
        if (Test-Path -LiteralPath (Join-Path $repositoryRoot '.env')) {
            throw 'Refusing to replace an existing environment file. CI must use a fresh checkout.'
        }
        Copy-Item -LiteralPath (Join-Path $repositoryRoot '.env.example') -Destination (Join-Path $repositoryRoot '.env')
        # Explicit CI addresses match the services exposed by the repository Compose file.
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        $env:USER_SERVICE_PORT = '5000'
        $env:MYSQL_PORT = '3306'
        $env:E2E_FRONTEND_URL = 'http://localhost:5173'
        $env:E2E_USER_SERVICE_URL = 'http://localhost:5000'
        $env:E2E_INVENTORY_SERVICE_URL = 'http://localhost:5001'
        $env:E2E_LENDING_SERVICE_URL = 'http://localhost:5148'
        $env:E2E_NOTIFICATION_SERVICE_URL = 'http://localhost:5003'
        $env:E2E_HEADLESS = 'true'
        $env:VITE_API_BASE_URL = $env:E2E_USER_SERVICE_URL
        $env:VITE_INVENTORY_API_BASE_URL = $env:E2E_INVENTORY_SERVICE_URL
        $env:VITE_LENDING_API_BASE_URL = $env:E2E_LENDING_SERVICE_URL
        $env:VITE_NOTIFICATION_API_BASE_URL = $env:E2E_NOTIFICATION_SERVICE_URL

        Push-Location $frontendRoot
        try {
            Invoke-Checked 'npm' @('ci', '--no-audit', '--no-fund')
            Invoke-Checked 'npm' @('run', 'build')
        }
        finally { Pop-Location }

        Invoke-Checked 'docker' @('info', '--format', '{{.OSType}}')
        $kafkaStarted = $true
        Invoke-Checked 'docker' @('compose', '-f', $kafkaComposeFile, 'up', '-d', '--wait', '--wait-timeout', '240', 'kafka')
        Invoke-Checked 'docker' @('compose', '-f', $kafkaComposeFile, 'run', '--rm', 'kafka-init')
        $stackStarted = $true
        $startArguments = @('compose', '-p', 'ase-e2e', '-f', $composeFile, 'up', '-d', '--build', 'mysql') + $services
        Invoke-Checked 'docker' $startArguments

        $frontendProcess = Start-Process -FilePath 'node' -WorkingDirectory $frontendRoot -ArgumentList @(
            'node_modules/vite/bin/vite.js', 'preview', '--host', '127.0.0.1', '--port', '5173', '--strictPort'
        ) -RedirectStandardOutput (Join-Path $ResultsDirectory 'frontend.stdout.log') -RedirectStandardError (Join-Path $ResultsDirectory 'frontend.stderr.log') -PassThru

        foreach ($baseUrl in @($env:E2E_USER_SERVICE_URL, $env:E2E_INVENTORY_SERVICE_URL, $env:E2E_LENDING_SERVICE_URL, $env:E2E_NOTIFICATION_SERVICE_URL)) {
            Wait-Endpoint "$baseUrl/health" -Health
        }
        Wait-Endpoint $env:E2E_FRONTEND_URL
        # /health alone does not prove User's migration succeeded (startup logs and continues on error).
        $adminRequest = @{ email = 'admin@library.com'; password = 'adminpassword' } | ConvertTo-Json
        $admin = Invoke-RestMethod -Method Post -Uri "$($env:E2E_USER_SERVICE_URL)/api/auth/login" -Body $adminRequest -ContentType 'application/json'
        if ($admin.role -ne 'Admin' -or !$admin.token) { throw 'CI admin login did not succeed.' }
        $authorization = @{ Authorization = "Bearer $($admin.token)" }
        Invoke-RestMethod -Uri "$($env:E2E_USER_SERVICE_URL)/api/Admin/users/pending" -Headers $authorization | Out-Null
    }

    $failedProjects = @()
    foreach ($project in $projects) {
        $projectFile = Join-Path $PSScriptRoot "$project.E2ETests/$project.E2ETests.csproj"
        $projectResults = Join-Path $ResultsDirectory $project
        $testArguments = @(
            'test', $projectFile, '--configuration', 'Release', '--no-build', '--no-restore',
            '--logger', "trx;LogFileName=$project.trx", '--results-directory', $projectResults,
            '--verbosity', 'minimal'
        )
        & dotnet @testArguments
        $testExitCode = $LASTEXITCODE
        $trxPath = Join-Path $projectResults "$project.trx"
        if (!(Test-Path -LiteralPath $trxPath)) {
            $failedProjects += $project
            continue
        }
        [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
        $counters = $trx.TestRun.ResultSummary.Counters
        if ($testExitCode -ne 0 -or [int]$counters.executed -eq 0 -or [int]$counters.failed -gt 0) {
            $failedProjects += $project
        }
    }
    if ($failedProjects.Count -gt 0) { throw "E2E projects failed: $($failedProjects -join ', '). See TRX artifacts." }
}
finally {
    if ($frontendProcess -and !$frontendProcess.HasExited) { Stop-Process -Id $frontendProcess.Id -ErrorAction SilentlyContinue }
    if ($stackStarted) {
        & docker compose -p ase-e2e -f $composeFile logs --no-color --tail 150 2>&1 |
            Out-File -LiteralPath (Join-Path $ResultsDirectory 'services.log') -Encoding utf8
        # The runner owns these disposable CI volumes; local existing-stack mode never reaches this.
        & docker compose -p ase-e2e -f $composeFile down --volumes --remove-orphans
    }
    if ($kafkaStarted) {
        & docker compose -f $kafkaComposeFile logs --no-color --tail 100 2>&1 |
            Out-File -LiteralPath (Join-Path $ResultsDirectory 'kafka.log') -Encoding utf8
        & docker compose -f $kafkaComposeFile down --volumes --remove-orphans
    }
    Pop-Location
}
