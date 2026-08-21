<#
.SYNOPSIS
    Duplica una campania de El Tejido llamando al API de administracion.

.DESCRIPTION
    El portal no expone el boton de duplicar, pero el endpoint existe:
    POST /api/admin/campanias/{id}/duplicar

    La autorizacion es por cookie de sesion (OTP de WhatsApp) mas el header
    X-CSRF-Token que devuelve el login. Este script hace las tres cosas:
    pide el codigo, canjea la sesion y llama al endpoint.

    La copia nace en estado Borrador, sin participantes y con el nombre
    "<original> (copia)". Con -NuevoNombre se renombra en el mismo paso.

.PARAMETER BaseUrl
    Raiz del ambiente. Por defecto produccion.

.PARAMETER Numero
    Numero de WhatsApp del administrador, con indicativo y sin signos.
    Ejemplo: 573182527390

.PARAMETER CampaniaId
    Id de la campania a duplicar. Si se omite, el script lista las campanias
    y pregunta cual.

.PARAMETER NuevoNombre
    Opcional. Renombra la copia inmediatamente despues de crearla.

.EXAMPLE
    .\duplicar-campania.ps1 -Numero 573182527390

.EXAMPLE
    .\duplicar-campania.ps1 -Numero 573182527390 -CampaniaId c_ab12... -NuevoNombre "Convencion 2026 - piloto"

.NOTES
    Recomendado ejecutarlo en PowerShell 7 (pwsh). En Windows PowerShell 5.1
    los nombres con tildes se ven mal por como decodifica la respuesta JSON;
    la operacion funciona igual, solo se ve feo en pantalla.
#>
[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://app-eltejido-prod-eus2-d9ebamhubdfqa9ac.eastus-01.azurewebsites.net',

    [Parameter(Mandatory = $true)]
    [string]$Numero,

    [string]$CampaniaId,

    [string]$NuevoNombre
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')

function Show-ErrorApi {
    param([System.Management.Automation.ErrorRecord]$Registro, [string]$Contexto)

    $detalle = $null
    if ($Registro.ErrorDetails -and $Registro.ErrorDetails.Message) {
        $detalle = $Registro.ErrorDetails.Message
    }

    Write-Host ""
    Write-Host "$Contexto fallo." -ForegroundColor Red
    if ($detalle) { Write-Host $detalle -ForegroundColor DarkYellow }
    else { Write-Host $Registro.Exception.Message -ForegroundColor DarkYellow }
}

# ---------------------------------------------------------------------------
# 1. Pedir el codigo OTP. La respuesta es neutral a proposito: dice lo mismo
#    exista o no el numero, asi que un "ok" aqui no confirma que el numero
#    este habilitado.
# ---------------------------------------------------------------------------
Write-Host "Pidiendo codigo para $Numero ..." -ForegroundColor Cyan
try {
    $cuerpo = @{ numero = $Numero } | ConvertTo-Json
    Invoke-RestMethod -Uri "$BaseUrl/api/auth/request-code" `
        -Method Post -Body $cuerpo -ContentType 'application/json' | Out-Null
}
catch {
    Show-ErrorApi $_ "La solicitud de codigo"
    Write-Host "Si el error es 429, esperá un momento: hay rate limit por IP." -ForegroundColor DarkYellow
    exit 1
}

$codigo = Read-Host "Codigo recibido por WhatsApp"

# ---------------------------------------------------------------------------
# 2. Canjear el codigo. Guarda la cookie de sesion en $sesion y el token CSRF,
#    que hace falta en toda mutacion.
# ---------------------------------------------------------------------------
try {
    $cuerpo = @{ numero = $Numero; codigo = $codigo } | ConvertTo-Json
    $login = Invoke-RestMethod -Uri "$BaseUrl/api/auth/verify-code" `
        -Method Post -Body $cuerpo -ContentType 'application/json' `
        -SessionVariable sesion
}
catch {
    Show-ErrorApi $_ "El canje del codigo"
    exit 1
}

$csrf = $login.csrfToken
$encabezados = @{ 'X-CSRF-Token' = $csrf }

Write-Host "Sesion abierta como $($login.usuario.nombre) [$($login.usuario.rol)]" -ForegroundColor Green
if ($login.usuario.rol -ne 'admin') {
    Write-Host "Ese usuario no es admin: el API va a rechazar la duplicacion." -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
# 3. Elegir la campania. Un GET solo necesita la cookie, no el CSRF.
# ---------------------------------------------------------------------------
if (-not $CampaniaId) {
    try {
        $lista = Invoke-RestMethod -Uri "$BaseUrl/api/admin/campanias?pageSize=100" `
            -Method Get -WebSession $sesion
    }
    catch {
        Show-ErrorApi $_ "El listado de campanias"
        exit 1
    }

    $lista.items | Select-Object id, nombre, estado | Format-Table -AutoSize
    $CampaniaId = Read-Host "Id de la campania a duplicar"
}

# ---------------------------------------------------------------------------
# 4. Duplicar. Sin cuerpo. Devuelve 201 con la campania nueva.
# ---------------------------------------------------------------------------
try {
    $copia = Invoke-RestMethod -Uri "$BaseUrl/api/admin/campanias/$CampaniaId/duplicar" `
        -Method Post -WebSession $sesion -Headers $encabezados
}
catch {
    Show-ErrorApi $_ "La duplicacion"
    exit 1
}

Write-Host ""
Write-Host "Copia creada:" -ForegroundColor Green
Write-Host "  id     : $($copia.id)"
Write-Host "  nombre : $($copia.nombre)"
Write-Host "  estado : $($copia.estado)"

# ---------------------------------------------------------------------------
# 5. Renombrar (opcional). El PUT es parcial en la practica: los campos que no
#    se mandan conservan su valor, no se borran.
# ---------------------------------------------------------------------------
if ($NuevoNombre) {
    try {
        $cuerpo = @{ nombre = $NuevoNombre } | ConvertTo-Json
        $copia = Invoke-RestMethod -Uri "$BaseUrl/api/admin/campanias/$($copia.id)" `
            -Method Put -Body $cuerpo -ContentType 'application/json' `
            -WebSession $sesion -Headers $encabezados
        Write-Host "  renombrada a: $($copia.nombre)" -ForegroundColor Green
    }
    catch {
        Show-ErrorApi $_ "El renombrado"
    }
}

Write-Host ""
Write-Host "Falta por hacer, en este orden:" -ForegroundColor Cyan
Write-Host "  1. Cargar el roster: la copia nace sin participantes."
Write-Host "     POST $BaseUrl/api/admin/usuarios/carga-masiva  (campaniaId = $($copia.id))"
Write-Host "     o    POST $BaseUrl/api/admin/campanias/$($copia.id)/participantes"
Write-Host "  2. Revisar rubricaRef y promptRefs: son referencias compartidas con la original."
Write-Host "  3. Activar al final. Borrador -> Activa no tiene vuelta atras."
Write-Host "     PATCH $BaseUrl/api/admin/campanias/$($copia.id)/estado  { estado: 'activa' }"
