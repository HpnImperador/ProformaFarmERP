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
