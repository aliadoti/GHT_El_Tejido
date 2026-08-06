# Helper QA E2E — Modalidad B (Azure). Dot-source en cada bloque: . <ruta>\_qa.ps1
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
[System.Net.ServicePointManager]::Expect100Continue = $false
[System.Net.ServicePointManager]::DefaultConnectionLimit = 20
$script:BASE = 'https://app-eltejido-mvp-evd8ffcgd3fthshw.eastus-01.azurewebsites.net'
$script:DIAG = 'mnJisidj/MwD97knRaUUDEBX2b5EcPPL7zjf9rZJm3rbQiKVZwxs51Ou1u4gB7GG'
$script:DH   = @{ 'X-Diag-Key' = $script:DIAG }
$script:ADMIN = '573001119999'

function J($o) { $o | ConvertTo-Json -Depth 12 -Compress }
# PS 5.1 corrompe UTF-8 en -Body string (usa codepage por defecto). Enviar SIEMPRE como bytes UTF-8.
function U8($json) { [System.Text.Encoding]::UTF8.GetBytes($json) }
$script:CT = 'application/json; charset=utf-8'

# Reintento para 400/5xx transitorios (cold-start de Azure App Service en la 1a peticion tras idle).
function Retry([scriptblock]$sb, [int]$max = 12) {
    for ($i = 1; ; $i++) {
        try { return & $sb }
        catch {
            $resp = $_.Exception.Response
            $code = if ($resp) { [int]$resp.StatusCode } else { 0 }
            $transitorio = ($code -in 400, 408, 429, 500, 502, 503, 504) -or ($code -eq 0)
            if ($i -ge $max -or -not $transitorio) { throw }
            Start-Sleep -Milliseconds ([Math]::Min(1500 * $i, 8000))
        }
    }
}

# Despierta la instancia/estabiliza la conexion antes de las llamadas reales.
function Warmup() { for ($k = 0; $k -lt 3; $k++) { try { Invoke-WebRequest -Uri "$script:BASE/health" -UseBasicParsing -TimeoutSec 25 | Out-Null; return } catch { Start-Sleep -Milliseconds 700 } } }

function Diag($path, $body) {
    Retry { Invoke-RestMethod -Method Post -Uri "$script:BASE$path" -Headers $script:DH -ContentType $script:CT -Body (U8 (J $body)) -UseBasicParsing }
}

# Inyecta un mensaje entrante (DT-QA-01). $mid = whatsappMessageId unico para evitar dedupe.
function Sim($numero, $texto, $mid) {
    $b = @{ numero = $numero; texto = $texto }
    if ($mid) { $b.whatsappMessageId = $mid }
    Retry { Invoke-RestMethod -Method Post -Uri "$script:BASE/diagnostico/simulacion/webhook-entrante" -Headers $script:DH -ContentType $script:CT -Body (U8 (J $b)) -UseBasicParsing }
}

function Connect() {
    Warmup
    Diag '/diagnostico/simulacion/admin-inicial' @{ numero = $script:ADMIN; nombre = 'Admin QA' } | Out-Null
    Diag '/diagnostico/simulacion/otp-admin' @{ numero = $script:ADMIN; codigo = '123456' } | Out-Null
    for ($i = 1; ; $i++) {
        try {
            $s = $null
            $r = Invoke-WebRequest -Method Post -Uri "$script:BASE/api/auth/verify-code" -ContentType $script:CT -Body (U8 (J @{ numero = $script:ADMIN; codigo = '123456' })) -SessionVariable s -UseBasicParsing
            $csrf = ($r.Content | ConvertFrom-Json).csrfToken
            return [pscustomobject]@{ s = $s; csrf = $csrf }
        } catch { if ($i -ge 4) { throw }; Start-Sleep -Milliseconds (600 * $i) }
    }
}

function G($ctx, $path) { Retry { Invoke-RestMethod -Method Get -Uri "$script:BASE$path" -WebSession $ctx.s -UseBasicParsing } }

function M($ctx, $method, $path, $body) {
    $p = @{ Method = $method; Uri = "$script:BASE$path"; WebSession = $ctx.s; Headers = @{ 'X-CSRF-Token' = $ctx.csrf }; UseBasicParsing = $true }
    if ($null -ne $body) { $p.ContentType = $script:CT; $p.Body = (U8 (J $body)) }
    Retry { Invoke-RestMethod @p }
}

# IDs de la corrida (persistidos por prep para reutilizar en cada caso)
$script:StateFile = Join-Path $PSScriptRoot 'run-state.json'
function Save-State($obj) { $obj | ConvertTo-Json -Depth 8 | Set-Content -Path $script:StateFile -Encoding UTF8 }
function Load-State() { if (Test-Path $script:StateFile) { Get-Content $script:StateFile -Raw | ConvertFrom-Json } else { $null } }
