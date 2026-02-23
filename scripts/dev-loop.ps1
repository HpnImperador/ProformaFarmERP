param(
    [switch]$NonInteractive = $true,
    [switch]$CreateBackup,
    [string]$BackupDir = "..\\backups",
    [string]$CreateBranch,
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$SkipTest,
    [string]$TestProject = "ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj",
    [string]$TestFilter,
    [switch]$UseLabSettings,
    [switch]$ApplyPostgresScripts,
    [string]$PostgresPsql = "psql",
    [string]$PostgresConnection,
    [switch]$PostgresSafeMode = $true,
    [string[]]$AllowedPostgresDatabases = @("proformafarm"),
    [string[]]$AllowedPostgresHosts = @(),
    [switch]$AcknowledgeSharedPostgres,
    [string]$CommitMessage,
    [switch]$Push,
    [string]$PushRef = "HEAD",
    [string]$PushRemote = "origin"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
if ($NonInteractive) {
    $ConfirmPreference = "None"
}

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Action
}

function Invoke-CommandChecked {
    param(
        [Parameter(Mandatory = $true)][string]$Command,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao executar: $Command $($Arguments -join ' ')"
    }
}

function Get-ConnectionValue {
    param(
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string[]]$Keys
    )

    foreach ($key in $Keys) {
        $pattern = "(?i)(?:^|\\s|;)$key\\s*=\\s*([^;\\s]+)"
        $match = [regex]::Match($ConnectionString, $pattern)
        if ($match.Success) {
            return $match.Groups[1].Value.Trim()
        }
    }

    return $null
}

function Assert-PostgresSafeMode {
    param(
        [Parameter(Mandatory = $true)][string]$PsqlCommand,
        [Parameter(Mandatory = $true)][string]$ConnectionString,
        [Parameter(Mandatory = $true)][string[]]$Databases,
        [Parameter(Mandatory = $true)][string[]]$Hosts,
        [Parameter(Mandatory = $true)][bool]$SharedAck
    )

    if (-not $SharedAck) {
        throw "Safe mode bloqueou a execução: informe -AcknowledgeSharedPostgres para confirmar que o servidor PostgreSQL é compartilhado."
    }

    $targetDb = Get-ConnectionValue -ConnectionString $ConnectionString -Keys @("dbname", "database")
    if ([string]::IsNullOrWhiteSpace($targetDb)) {
        throw "Safe mode bloqueou a execução: não foi possível identificar o database na conexão PostgreSQL."
    }

    $isDbAllowed = $false
    foreach ($db in $Databases) {
        if ($targetDb.Equals($db, [System.StringComparison]::OrdinalIgnoreCase)) {
            $isDbAllowed = $true
            break
        }
    }
    if (-not $isDbAllowed) {
        throw "Safe mode bloqueou a execução: database alvo '$targetDb' não está na allowlist: $($Databases -join ', ')."
    }

    $targetHost = Get-ConnectionValue -ConnectionString $ConnectionString -Keys @("host", "server")
    if ($Hosts.Count -gt 0) {
        if ([string]::IsNullOrWhiteSpace($targetHost)) {
            throw "Safe mode bloqueou a execução: host não identificado na conexão PostgreSQL."
        }

        $isHostAllowed = $false
        foreach ($host in $Hosts) {
            if ($targetHost.Equals($host, [System.StringComparison]::OrdinalIgnoreCase)) {
                $isHostAllowed = $true
                break
            }
        }
        if (-not $isHostAllowed) {
            throw "Safe mode bloqueou a execução: host alvo '$targetHost' não está na allowlist: $($Hosts -join ', ')."
        }
    }

    $probeArgs = @($ConnectionString, "-t", "-A", "-c", "select current_database();")
    $probeOutput = & $PsqlCommand @probeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Safe mode bloqueou a execução: falha ao validar conexão com psql."
    }

    $runtimeDb = ($probeOutput | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($runtimeDb)) {
        throw "Safe mode bloqueou a execução: current_database() retornou vazio."
    }

    if (-not $runtimeDb.Equals($targetDb, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Safe mode bloqueou a execução: database em runtime '$runtimeDb' difere do alvo '$targetDb'."
    }

    Write-Host "Safe mode PostgreSQL validado (db=$runtimeDb host=$targetHost)." -ForegroundColor Yellow
}

function New-ProjectBackup {
    param(
        [Parameter(Mandatory = $true)][string]$TargetDir
    )

    $ts = Get-Date -Format "yyyyMMdd_HHmmss"
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null

    $bundle = Join-Path $TargetDir "ProformaFarmERP_repo_$ts.bundle"
    $zip = Join-Path $TargetDir "ProformaFarmERP_workspace_$ts.zip"

    Invoke-CommandChecked -Command "git" -Arguments @("bundle", "create", $bundle, "--all")

    if (Test-Path $zip) {
        Remove-Item $zip -Force
    }
    Compress-Archive -Path * -DestinationPath $zip -CompressionLevel Optimal -Force

    Write-Host "Backup criado:" -ForegroundColor Green
    Write-Host " - $bundle"
    Write-Host " - $zip"
}

Write-Host "ProformaFarmERP dev-loop iniciado..." -ForegroundColor Green

if ($CreateBackup) {
    Invoke-Step -Name "Backup geral (bundle + zip)" -Action {
        New-ProjectBackup -TargetDir $BackupDir
    }
}

if (-not [string]::IsNullOrWhiteSpace($CreateBranch)) {
    Invoke-Step -Name "git checkout -b $CreateBranch" -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("checkout", "-b", $CreateBranch)
    }
}

if (-not $SkipRestore) {
    Invoke-Step -Name "dotnet restore" -Action {
        Invoke-CommandChecked -Command "dotnet" -Arguments @("restore")
    }
}

if (-not $SkipBuild) {
    Invoke-Step -Name "dotnet build" -Action {
        if ($UseLabSettings) {
            Invoke-CommandChecked -Command "dotnet" -Arguments @("build", "--configuration", "Debug", "--property:ASPNETCORE_ENVIRONMENT=Lab")
        }
        else {
            Invoke-CommandChecked -Command "dotnet" -Arguments @("build")
        }
    }
}

if ($ApplyPostgresScripts) {
    if ([string]::IsNullOrWhiteSpace($PostgresConnection)) {
        throw "Informe -PostgresConnection para aplicar scripts PostgreSQL."
    }

    if ($PostgresSafeMode) {
        Invoke-Step -Name "Validação Safe Mode PostgreSQL" -Action {
            Assert-PostgresSafeMode `
                -PsqlCommand $PostgresPsql `
                -ConnectionString $PostgresConnection `
                -Databases $AllowedPostgresDatabases `
                -Hosts $AllowedPostgresHosts `
                -SharedAck $AcknowledgeSharedPostgres.IsPresent
        }
    }

    $scripts = @(
        "docs/sql/postgresql/001_estrutura_organizacional_postgresql.sql",
        "docs/sql/postgresql/002_seed_estrutura_organizacional_postgresql.sql",
        "docs/sql/postgresql/003_idx_lotacaousuario_orgcontext_postgresql.sql",
        "docs/sql/postgresql/004_estoque_basico_postgresql.sql",
        "docs/sql/postgresql/005_core_outbox_postgresql.sql",
        "docs/sql/postgresql/006_integration_event_relay_postgresql.sql"
    )

    foreach ($script in $scripts) {
        Invoke-Step -Name "Aplicando $script" -Action {
            Invoke-CommandChecked -Command $PostgresPsql -Arguments @($PostgresConnection, "-v", "ON_ERROR_STOP=1", "-f", $script)
        }
    }
}

if (-not $SkipTest) {
    Invoke-Step -Name "dotnet test" -Action {
        $args = @("test", $TestProject)
        if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
            $args += @("--filter", $TestFilter)
        }
        Invoke-CommandChecked -Command "dotnet" -Arguments $args
    }
}

if (-not [string]::IsNullOrWhiteSpace($CommitMessage)) {
    Invoke-Step -Name "git add ." -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("add", ".")
    }

    Invoke-Step -Name "git commit" -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("commit", "-m", $CommitMessage, "--no-verify")
    }
}

if ($Push) {
    Invoke-Step -Name "git push" -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("push", $PushRemote, $PushRef)
    }
}

Invoke-Step -Name "git status --short" -Action {
    Invoke-CommandChecked -Command "git" -Arguments @("status", "--short")
}

Write-Host ""
Write-Host "Dev-loop concluido com sucesso (modo nao interativo)." -ForegroundColor Green
