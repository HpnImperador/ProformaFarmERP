param(
    [string]$RepoPath = "e:\Dev 2026\repositorio\ProformaFarm\ProformaFarmERP",
    [string]$Remote = "origin",
    [string]$GitExe = "E:\Meus Programas\Git\cmd\git.exe"
)

$ErrorActionPreference = "Stop"

$logDir = Join-Path $RepoPath "logs"
if (-not (Test-Path $logDir)) {
    New-Item -ItemType Directory -Path $logDir -Force | Out-Null
}

$logFile = Join-Path $logDir "auto-push.log"
$timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

try {
    Set-Location $RepoPath

    $branch = (& $GitExe branch --show-current).Trim()
    if ([string]::IsNullOrWhiteSpace($branch)) {
        Add-Content -Path $logFile -Value "[$timestamp] ERRO: branch atual não identificada."
        exit 1
    }

    $upstream = "$Remote/$branch"

    & $GitExe rev-parse --verify $upstream *> $null
    if ($LASTEXITCODE -ne 0) {
        Add-Content -Path $logFile -Value "[$timestamp] INFO: upstream '$upstream' não encontrado. Tentando push inicial."
        & $GitExe push $Remote $branch *> $null
        if ($LASTEXITCODE -eq 0) {
            Add-Content -Path $logFile -Value "[$timestamp] OK: push inicial executado para $upstream."
            exit 0
        }

        Add-Content -Path $logFile -Value "[$timestamp] ERRO: falha no push inicial para $upstream."
        exit 1
    }

    $countsRaw = (& $GitExe rev-list --left-right --count "$upstream...$branch").Trim()
    $parts = ($countsRaw -split "`t")
    if ($parts.Count -lt 2) {
        $parts = ($countsRaw -split " ") | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    }

    $behind = [int]$parts[0]
    $ahead = [int]$parts[1]

    if ($ahead -le 0) {
        Add-Content -Path $logFile -Value "[$timestamp] INFO: sem commits pendentes para push na branch '$branch'."
        exit 0
    }

    & $GitExe push $Remote $branch *> $null
    if ($LASTEXITCODE -eq 0) {
        Add-Content -Path $logFile -Value "[$timestamp] OK: push executado em '$upstream' (ahead=$ahead, behind=$behind)."
        exit 0
    }

    Add-Content -Path $logFile -Value "[$timestamp] ERRO: falha ao executar push em '$upstream'."
    exit 1
}
catch {
    Add-Content -Path $logFile -Value "[$timestamp] ERRO: $($_.Exception.Message)"
    exit 1
}
