# Backup del ProjectVTT (base de datos + mapas). Uso: ProjectVTT-backup.ps1
# Crea la copia en la carpeta de backups (por defecto %TEMP%\projectvtt-backups).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $env:ConnectionStrings__Default) {
    $env:Database__Provider = 'sqlite'
    $env:ConnectionStrings__Default = "Data Source=$env:TEMP\projectvtt-demo.db"
}
dotnet run --project (Join-Path $root 'tools\ProjectVTT.Tools') --no-launch-profile -- backup
