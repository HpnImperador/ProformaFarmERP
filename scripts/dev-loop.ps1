param(
    [switch]$SkipRestore,
    [switch]$SkipBuild,
    [switch]$SkipTest,
    [string]$CommitMessage,
    [switch]$Push,
    [string]$PushRef = "HEAD",
    [string]$PushRemote = "origin"
)

$ErrorActionPreference = "Stop"

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

Write-Host "ProformaFarmERP dev-loop iniciado..." -ForegroundColor Green

if (-not $SkipRestore) {
    Invoke-Step -Name "dotnet restore" -Action {
        Invoke-CommandChecked -Command "dotnet" -Arguments @("restore")
    }
}

if (-not $SkipBuild) {
    Invoke-Step -Name "dotnet build" -Action {
        Invoke-CommandChecked -Command "dotnet" -Arguments @("build")
    }
}

if (-not $SkipTest) {
    Invoke-Step -Name "dotnet test" -Action {
        Invoke-CommandChecked -Command "dotnet" -Arguments @("test")
    }
}

if (-not [string]::IsNullOrWhiteSpace($CommitMessage)) {
    Invoke-Step -Name "git add ." -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("add", ".")
    }

    Invoke-Step -Name "git commit" -Action {
        Invoke-CommandChecked -Command "git" -Arguments @("commit", "-m", $CommitMessage)
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
Write-Host "Dev-loop concluido com sucesso." -ForegroundColor Green
