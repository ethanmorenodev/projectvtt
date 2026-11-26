# ProjectVTT: crea/actualiza el acceso directo de escritorio con el logo de la
# app y la opción «Ejecutar como administrador» activada (permiso UAC).
# Idempotente: se puede volver a ejecutar cuando cambie la ruta del proyecto.
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\ProjectVTT-acceso-directo.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$desktop = [Environment]::GetFolderPath('Desktop')
$linkPath = Join-Path $desktop 'ProjectVTT (iniciar).lnk'
$iconPath = Join-Path $root 'apps\web\public\favicon.ico'
$startup = Join-Path $root 'scripts\ProjectVTT-inicio.ps1'
$powershell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'

foreach ($required in @($iconPath, $startup, $powershell)) {
    if (-not (Test-Path $required)) {
        throw "Falta el archivo requerido: $required"
    }
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($linkPath)
$shortcut.TargetPath = $powershell
$shortcut.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$startup`""
$shortcut.WorkingDirectory = $root
$shortcut.IconLocation = "$iconPath,0"
$shortcut.Description = 'ProjectVTT: iniciar servidor y cliente web (como administrador)'
$shortcut.Save()

# WScript.Shell no expone «Ejecutar como administrador»: se activa el bit
# SLDF_RUNAS_USER del encabezado ShellLink (byte 0x15, máscara 0x20).
$bytes = [System.IO.File]::ReadAllBytes($linkPath)
if ($bytes.Length -le 0x15) {
    throw "El acceso directo generado es demasiado corto: $linkPath"
}
$bytes[0x15] = $bytes[0x15] -bor 0x20
[System.IO.File]::WriteAllBytes($linkPath, $bytes)

Write-Host "Acceso directo listo: $linkPath"
Write-Host "  Icono: $iconPath"
Write-Host "  Ejecutar como administrador: activado"
