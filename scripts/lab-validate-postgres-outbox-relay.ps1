param(
    [Parameter(Mandatory = $true)][string]$PostgresHost,
    [int]$PostgresPort = 5432,
    [Parameter(Mandatory = $true)][string]$PostgresDatabase,
    [Parameter(Mandatory = $true)][string]$PostgresUser,
    [Parameter(Mandatory = $true)][string]$PostgresPassword,
    [string]$PsqlPath = "psql",
    [string]$TestProject = "ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj",
    [string]$LogsDir = "logs",
    [switch]$SkipBuild = $false
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

if (-not (Test-Path $LogsDir)) {
    New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logFile = Join-Path $LogsDir "lab-postgres-outbox-relay-$timestamp.log"
$conn = "host=$PostgresHost port=$PostgresPort dbname=$PostgresDatabase user=$PostgresUser password=$PostgresPassword"

$devLoopArgs = @(
    "-ExecutionPolicy", "Bypass",
    "-File", "scripts/dev-loop.ps1",
    "-ValidatePostgresOutboxRelay",
    "-PostgresPsql", $PsqlPath,
    "-PostgresConnection", $conn,
    "-AllowedPostgresDatabases", $PostgresDatabase,
    "-AllowedPostgresHosts", $PostgresHost,
    "-AcknowledgeSharedPostgres",
    "-TestProject", $TestProject
)

if ($SkipBuild) {
    $devLoopArgs += "-SkipBuild"
}

Write-Host "Executando validação do Outbox/Event Relay no PostgreSQL..." -ForegroundColor Cyan
Write-Host "Log: $logFile" -ForegroundColor Yellow

powershell @devLoopArgs 2>&1 | Tee-Object -FilePath $logFile
if ($LASTEXITCODE -ne 0) {
    throw "Validação falhou. Consulte o log: $logFile"
}

Write-Host ""
Write-Host "Validação concluída com sucesso." -ForegroundColor Green
Write-Host "Evidência salva em: $logFile" -ForegroundColor Green
