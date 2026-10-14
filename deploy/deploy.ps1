<#
=======================================================================
 ESGImpact - deploy de um ambiente (staging ou production)
-----------------------------------------------------------------------
 Chamado pelo pipeline no runner self-hosted (Windows + Docker Desktop).
 Tambem pode ser executado manualmente:

   $env:API_IMAGE = "ghcr.io/jaowilqui/esgimpact-api:sha-abc1234"
   $env:MONGO_ROOT_PASSWORD = "..."
   $env:JWT_KEY = "..."
   powershell -ExecutionPolicy Bypass -File deploy\deploy.ps1 -Environment staging

 Etapas:
   1. docker compose pull + up --wait   (projeto isolado por ambiente)
   2. smoke tests HTTP contra o ambiente recem-implantado
   3. resumo no GitHub Actions (GITHUB_STEP_SUMMARY)
 Em caso de falha imprime o estado e os logs dos containers e sai com 1.

 Compativel com Windows PowerShell 5.1 e PowerShell 7+.
=======================================================================
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('staging', 'production')]
    [string]$Environment,

    [string]$ExpectedCommit = $env:GIT_SHA
)

$ErrorActionPreference = 'Stop'

$root        = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $root 'deploy\docker-compose.deploy.yml'
$envFile     = Join-Path $root "deploy\$Environment.env"
$project     = "esgimpact-$Environment"

if (-not $env:API_IMAGE) { throw 'Variavel API_IMAGE nao definida.' }
if (-not $env:MONGO_ROOT_PASSWORD) { throw 'Segredo MONGO_ROOT_PASSWORD nao definido.' }
if (-not $env:JWT_KEY) { throw 'Segredo JWT_KEY nao definido.' }

# --- le o env file para descobrir porta e nome esperado do ambiente ----
$cfg = @{}
Get-Content $envFile | Where-Object { $_ -match '^\s*[A-Z_]+=' } | ForEach-Object {
    $k, $v = $_ -split '=', 2
    $cfg[$k.Trim()] = $v.Trim()
}
$port    = $cfg['API_PORT']
$envName = $cfg['ASPNETCORE_ENVIRONMENT']
$baseUrl = "http://localhost:$port"

# O docker escreve progresso em stderr. No Windows PowerShell 5.1, com
# ErrorActionPreference=Stop, isso viraria erro; por isso a saida e
# convertida em texto e o resultado e decidido apenas pelo exit code.
function Invoke-Native {
    param([string[]]$NativeArgs)
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & docker @NativeArgs 2>&1 | ForEach-Object { Write-Host "$_" }
        return $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $previous }
}

function Invoke-Compose {
    param([string[]]$ComposeArgs)
    $code = Invoke-Native (@('compose', '-p', $project, '--env-file', $envFile, '-f', $composeFile) + $ComposeArgs)
    if ($code -ne 0) { throw "docker compose $($ComposeArgs -join ' ') falhou (codigo $code)." }
}

function Show-Diagnostics {
    Write-Host '::group::Estado dos containers'
    $null = Invoke-Native @('compose', '-p', $project, '--env-file', $envFile, '-f', $composeFile, 'ps')
    Write-Host '::endgroup::'
    Write-Host '::group::Logs (ultimas 120 linhas)'
    $null = Invoke-Native @('compose', '-p', $project, '--env-file', $envFile, '-f', $composeFile, 'logs', '--tail', '120')
    Write-Host '::endgroup::'
}

function Invoke-WithRetry {
    param([scriptblock]$Action, [string]$Name, [int]$Attempts = 12, [int]$DelaySeconds = 5)
    for ($i = 1; $i -le $Attempts; $i++) {
        try { return & $Action }
        catch {
            if ($i -eq $Attempts) { throw "Smoke test '$Name' falhou: $($_.Exception.Message)" }
            Write-Host "  [$Name] tentativa $i/$Attempts falhou - aguardando ${DelaySeconds}s"
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$check, [string]$detail) {
    $results.Add([pscustomobject]@{ Check = $check; Detail = $detail })
    Write-Host "  OK  $check - $detail"
}

try {
    Write-Host "== Deploy de $Environment =="
    Write-Host "   imagem : $($env:API_IMAGE)"
    Write-Host "   projeto: $project"
    Write-Host "   url    : $baseUrl"

    Invoke-Compose @('pull', 'api')
    Invoke-Compose @('up', '-d', '--remove-orphans', '--wait', '--wait-timeout', '240')

    Write-Host '== Smoke tests =='

    $health = Invoke-WithRetry -Name 'health' -Action {
        $r = Invoke-RestMethod -Uri "$baseUrl/api/health" -TimeoutSec 10
        if ($r.status -ne 'ok' -or $r.mongo -ne 'up') { throw "resposta inesperada: $($r | ConvertTo-Json -Compress)" }
        $r
    }
    Add-Result 'GET /api/health' "status=$($health.status), mongo=$($health.mongo)"

    $info = Invoke-RestMethod -Uri "$baseUrl/api/info" -TimeoutSec 10
    if ($info.environment -ne $envName) { throw "ambiente esperado '$envName', recebido '$($info.environment)'" }
    if ($ExpectedCommit -and $info.commit -ne $ExpectedCommit) { throw "commit esperado '$ExpectedCommit', recebido '$($info.commit)'" }
    Add-Result 'GET /api/info' "environment=$($info.environment), version=$($info.version), commit=$($info.commit)"

    $stats = Invoke-WithRetry -Name 'carga inicial' -Action {
        $s = Invoke-RestMethod -Uri "$baseUrl/api/collections/stats" -TimeoutSec 10
        if ([int64]$s.collections.emission_factors -lt 12) { throw "emission_factors=$($s.collections.emission_factors)" }
        $s
    }
    $c = $stats.collections
    Add-Result 'GET /api/collections/stats' ("db={0}; emission_factors={1}, simulations={2}, participants={3}, offset_projects={4}, governance_audits={5}" -f `
        $stats.database, $c.emission_factors, $c.simulations, $c.participants, $c.offset_projects, $c.governance_audits)

    $login = Invoke-RestMethod -Uri "$baseUrl/api/auth/login" -Method Post -ContentType 'application/json' `
        -Body (@{ username = 'analista'; password = 'Esg@2026' } | ConvertTo-Json) -TimeoutSec 10
    if (-not $login.accessToken) { throw 'login nao retornou token' }
    Add-Result 'POST /api/auth/login' "role=$($login.role)"

    $dash = Invoke-RestMethod -Uri "$baseUrl/api/analytics/dashboard" -Headers @{ Authorization = "Bearer $($login.accessToken)" } -TimeoutSec 10
    Add-Result 'GET /api/analytics/dashboard' "totalSimulations=$($dash.totalSimulations), totalKgCo2=$($dash.totalKgCo2)"

    Write-Host "== $Environment implantado com sucesso em $baseUrl =="

    if ($env:GITHUB_STEP_SUMMARY) {
        $md = @()
        $md += "### Deploy em **$Environment** concluido"
        $md += ''
        $md += "| Item | Valor |"
        $md += "|---|---|"
        $md += "| Imagem | ``$($env:API_IMAGE)`` |"
        $md += "| URL | $baseUrl/swagger |"
        $md += "| Ambiente ASP.NET | $($info.environment) |"
        $md += "| Commit | $($info.commit) |"
        $md += ''
        $md += '| Smoke test | Resultado |'
        $md += '|---|---|'
        foreach ($r in $results) { $md += "| $($r.Check) | $($r.Detail) |" }
        $md -join "`n" | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8
    }
}
catch {
    Write-Host "::error::Deploy de $Environment falhou: $($_.Exception.Message)"
    Show-Diagnostics
    exit 1
}
