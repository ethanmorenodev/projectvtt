# Restaura un backup del ProjectVTT (base + mapas).
# Uso: ProjectVTT-restore.ps1 <backupId>   (ej.: backup-20261109-133000000)
# IMPORTANTE: la restauración pisa los archivos; el script detiene el
# servidor si está en marcha y hay que volver a arrancarlo después.
param(
    [Parameter(Mandatory = $true)]
    [string]$BackupId
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$conn = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($conn) {
    Stop-Process -Id $conn.OwningProcess -Force
    Write-Output "Servidor detenido para la restauracion."
}

if (-not $env:ConnectionStrings__Default) {
    $env:Database__Provider = 'sqlite'
    $env:ConnectionStrings__Default = "Data Source=$env:TEMP\projectvtt-demo.db"
}
dotnet run --project (Join-Path $root 'tools\ProjectVTT.Tools') --no-launch-profile -- restore $BackupId
Write-Output "Listo. Vuelve a arrancar la aplicacion con 'ProjectVTT (iniciar)'."
