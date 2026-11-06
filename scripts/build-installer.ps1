Param(
	[string]$Configuration = "Release",
	[string]$Runtime = "win-x64",
	[string]$Version = "0.1.0"
)

$solutionDir = Split-Path -Parent $PSScriptRoot
Push-Location $solutionDir

Write-Host "Publishing project to produce files (Configuration=$Configuration, Runtime=$Runtime)..."
& powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-zip.ps1 -Configuration $Configuration -Runtime $Runtime
if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; Pop-Location; exit 1 }

$publishDir = Join-Path $solutionDir "artifacts\GameFocus-$Runtime"
if (-not (Test-Path $publishDir)) { Write-Error "Publish folder not found: $publishDir"; Pop-Location; exit 1 }

$iss = Join-Path $solutionDir "installer\GameFocusInstaller.iss"
if (-not (Test-Path $iss)) { Write-Error "Inno Setup script not found: $iss"; Pop-Location; exit 1 }

Write-Host "Looking for Inno Setup Compiler (ISCC.exe)..."
$possible = @(
	"$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
	"$env:ProgramFiles\Inno Setup 6\ISCC.exe",
	"$env:ISCC"
)

$iscc = $possible | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
	Write-Host "ISCC.exe not found. Please install Inno Setup (https://jrsoftware.org/isinfo.php) and ensure ISCC.exe is on PATH or set environment variable ISCC pointing to it." -ForegroundColor Yellow
	Pop-Location
	exit 2
}

Write-Host "Found ISCC: $iscc"

# Ensure output dir exists
$installerOut = Join-Path $solutionDir "artifacts\installer"
New-Item -ItemType Directory -Path $installerOut -Force | Out-Null

Write-Host "Compiling installer..."
& "$iscc" /O"$installerOut" /F"GameFocus-$Version-Setup" "$iss"
if ($LASTEXITCODE -ne 0) { Write-Error "ISCC failed"; Pop-Location; exit 3 }

Write-Host "Installer created in: $installerOut"
Pop-Location
