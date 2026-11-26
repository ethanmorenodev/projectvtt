# ProjectVTT: arranca el backend (SQLite) y el cliente web, y abre la
# aplicacion. Idempotente: si algo ya esta en marcha no lo duplica.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts/ProjectVTT-inicio.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

function Test-Port([int]$port) {
    [bool](Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
}

function Test-Health([string]$url) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

$apiUrl = 'http://localhost:5000'

# Backend (API) en http://localhost:5000 con SQLite local.
if (-not (Test-Port 5000)) {
    $env:Database__Provider = 'sqlite'
    $env:ConnectionStrings__Default = "Data Source=$env:TEMP\projectvtt-demo.db"
    $env:ASPNETCORE_URLS = $apiUrl
    # La BD demo es desechable: si el modelo cambio, el backend guarda una
    # copia .bak y la regenera en vez de morir al sembrar (de ahi el 502).
    $env:PROJECTVTT_RESET_DEMO_DB_ON_MISMATCH = '1'
    Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/c', 'dotnet run --project backend/Api --no-launch-profile' `
        -WorkingDirectory $root -WindowStyle Minimized
} else {
    $env:PROJECTVTT_RESET_DEMO_DB_ON_MISMATCH = '1'
}

# Cliente web (Vite) en http://localhost:5173.
if (-not (Test-Port 5173)) {
    Start-Process -FilePath 'cmd.exe' `
        -ArgumentList '/c', 'npm run dev' `
        -WorkingDirectory (Join-Path $root 'apps\web') -WindowStyle Minimized
}

# Esperar a que la API responda /health (no basta con que el puerto escuche:
# una BD desactualizada hace que el proceso muera justo despues y el proxy
# devuelva 502). Si no salud en 60 s, no abrir el navegador.
$apiReady = $false
foreach ($i in 1..120) {
    if (Test-Health "$apiUrl/health") { $apiReady = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $apiReady) {
    Write-Error 'La API no respondio /health en 60 s. Revisa la ventana minimizada de dotnet; no se abre el navegador para evitar un 502.'
    exit 1
}

$webReady = $false
foreach ($i in 1..60) {
    if (Test-Port 5173) { $webReady = $true; break }
    Start-Sleep -Milliseconds 500
}
if (-not $webReady) {
    Write-Error 'El cliente web no arranco en 30 s (puerto 5173).'
    exit 1
}

# Abrir la aplicacion: ventana propia con Edge si existe; navegador por
# defecto en caso contrario.
$edge = $null
foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
        "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
        "$env:LOCALAPPDATA\Microsoft\Edge\Application\msedge.exe")) {
    if (Test-Path $candidate) {
        $edge = $candidate
        break
    }
}

if ($edge) {
    Start-Process -FilePath $edge -ArgumentList '--app=http://localhost:5173/'
} else {
    Start-Process 'http://localhost:5173/'
}
