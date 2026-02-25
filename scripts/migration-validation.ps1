param(
    [Parameter(Mandatory = $true)][string]$PostgresHost,
    [int]$PostgresPort = 5432,
    [Parameter(Mandatory = $true)][string]$PostgresDatabase,
    [Parameter(Mandatory = $true)][string]$PostgresUser,
    [Parameter(Mandatory = $true)][string]$PostgresPassword,
    [string]$PsqlPath = "psql",
    [string]$TestProject = "ProformaFarm.Application.Tests/ProformaFarm.Application.Tests.csproj",
    [string]$TestFilter = "FullyQualifiedName~Integration.Auth.LoginEndpointTests",
    [string]$LogsDir = "logs",
    [switch]$SkipApplyScripts,
    [switch]$IncludeOutboxValidation
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

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

if (-not (Test-Path $LogsDir)) {
    New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$conn = "host=$PostgresHost port=$PostgresPort dbname=$PostgresDatabase user=$PostgresUser password=$PostgresPassword"
$txtReport = Join-Path $LogsDir "migration-validation-$timestamp.log"
$mdReport = Join-Path $LogsDir "migration-validation-$timestamp.md"

Write-Host "Iniciando validacao de migracao PostgreSQL..." -ForegroundColor Green
Write-Host "Relatorio TXT: $txtReport" -ForegroundColor Yellow
Write-Host "Relatorio MD : $mdReport" -ForegroundColor Yellow

@(
    "# Relatorio de Validacao de Migracao PostgreSQL"
    ""
    "- Data: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")"
    "- Host: $PostgresHost"
    "- Porta: $PostgresPort"
    "- Database: $PostgresDatabase"
    "- Projeto de testes: $TestProject"
    "- Filtro de testes: $TestFilter"
    ""
) | Set-Content -Path $mdReport -Encoding UTF8

Invoke-Step -Name "Precheck psql e conectividade" -Action {
    $env:PGPASSWORD = $PostgresPassword
    $precheck = & $PsqlPath -h $PostgresHost -p $PostgresPort -U $PostgresUser -d $PostgresDatabase -w -t -A -c "select current_database() || '|' || version();"
    if ($LASTEXITCODE -ne 0) {
        throw "Falha no precheck PostgreSQL."
    }
    $line = ($precheck | Select-Object -Last 1).Trim()
    "Precheck: $line" | Tee-Object -FilePath $txtReport -Append | Out-Null
    @(
        "## 1) Precheck"
        ""
        "- Resultado: OK"
        "- Detalhe: $line"
        ""
    ) | Add-Content -Path $mdReport -Encoding UTF8
}

if (-not $SkipApplyScripts) {
    Invoke-Step -Name "Aplicacao de scripts idempotentes PostgreSQL" -Action {
        $args = @(
            "-ExecutionPolicy", "Bypass",
            "-File", "scripts/dev-loop.ps1",
            "-SkipBuild",
            "-SkipTest",
            "-ApplyPostgresScripts",
            "-PostgresPsql", $PsqlPath,
            "-PostgresConnection", $conn,
            "-AllowedPostgresDatabases", $PostgresDatabase,
            "-AllowedPostgresHosts", $PostgresHost,
            "-AcknowledgeSharedPostgres"
        )

        $oldEap = $ErrorActionPreference
        try {
            $ErrorActionPreference = "Continue"
            & powershell @args 2>&1 | Tee-Object -FilePath $txtReport -Append
            if ($LASTEXITCODE -ne 0) {
                throw "Falha na aplicacao de scripts PostgreSQL."
            }
        }
        finally {
            $ErrorActionPreference = $oldEap
        }

        @(
            "## 2) Aplicacao de Scripts"
            ""
            "- Resultado: OK"
            "- Modo: idempotente via scripts/dev-loop.ps1 -ApplyPostgresScripts"
            ""
        ) | Add-Content -Path $mdReport -Encoding UTF8
    }
}

Invoke-Step -Name "Teste de readiness PostgreSQL (Auth)" -Action {
    $oldProvider = $env:Database__Provider
    $oldPg = $env:ConnectionStrings__PostgresConnection

    try {
        $env:Database__Provider = "PostgreSql"
        $env:ConnectionStrings__PostgresConnection = $conn
        & dotnet test $TestProject --filter $TestFilter 2>&1 | Tee-Object -FilePath $txtReport -Append
        if ($LASTEXITCODE -ne 0) {
            throw "Falha no dotnet test com provider PostgreSQL."
        }
    }
    finally {
        $env:Database__Provider = $oldProvider
        $env:ConnectionStrings__PostgresConnection = $oldPg
    }

    @(
        "## 3) Testes de Readiness"
        ""
        "- Resultado: OK"
        "- Provider: PostgreSql"
        "- Escopo: autenticacao (base obrigatoria para todos os fluxos)"
        ""
    ) | Add-Content -Path $mdReport -Encoding UTF8
}

if ($IncludeOutboxValidation) {
    Invoke-Step -Name "Validacao dedicada Outbox/Event Relay" -Action {
        $args = @(
            "-ExecutionPolicy", "Bypass",
            "-File", "scripts/lab-validate-postgres-outbox-relay.ps1",
            "-PostgresHost", $PostgresHost,
            "-PostgresPort", "$PostgresPort",
            "-PostgresDatabase", $PostgresDatabase,
            "-PostgresUser", $PostgresUser,
            "-PostgresPassword", $PostgresPassword,
            "-PsqlPath", $PsqlPath,
            "-TestProject", $TestProject
        )

        $oldEap = $ErrorActionPreference
        try {
            $ErrorActionPreference = "Continue"
            & powershell @args 2>&1 | Tee-Object -FilePath $txtReport -Append
            if ($LASTEXITCODE -ne 0) {
                throw "Falha na validacao Outbox/Event Relay."
            }
        }
        finally {
            $ErrorActionPreference = $oldEap
        }

        @(
            "## 4) Outbox/Event Relay"
            ""
            "- Resultado: OK"
            "- Modo: validacao dedicada + snapshot pre/pós"
            ""
        ) | Add-Content -Path $mdReport -Encoding UTF8
    }
}

@(
    "## 5) Conclusao"
    ""
    "- Status geral: APROVADO"
    "- Relatorio tecnico completo: $(Split-Path -Leaf $txtReport)"
    ""
) | Add-Content -Path $mdReport -Encoding UTF8

Write-Host ""
Write-Host "Validacao concluida com sucesso." -ForegroundColor Green
Write-Host "TXT: $txtReport" -ForegroundColor Green
Write-Host "MD : $mdReport" -ForegroundColor Green
