<#
Copies the application's SQLite database to a timestamped backup file.
Run from repository root.
#>
$dbPath = Join-Path $env:LOCALAPPDATA "GameFocus\gamefocus.db"
if (-not (Test-Path $dbPath)) {
	Write-Error "Database not found at $dbPath"
	exit 1
}

$backupDir = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts\backups"
New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

$timestamp = (Get-Date).ToString('yyyyMMdd_HHmmss')
$dest = Join-Path $backupDir "gamefocus_backup_$timestamp.db"
Copy-Item -Path $dbPath -Destination $dest -Force
Write-Host "Database backed up to: $dest"
