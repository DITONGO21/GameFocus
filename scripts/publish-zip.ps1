Param(
	[string]$Configuration = "Release",
	[string]$Runtime = "win-x64"
)

$solutionDir = Split-Path -Parent $PSScriptRoot
Push-Location $solutionDir

Write-Host "Restoring and publishing GameFocus.UI ($Runtime / $Configuration)..."
dotnet restore "$solutionDir\GameFocus.slnx"

$out = "$solutionDir\artifacts\GameFocus-$Runtime"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue $out
dotnet publish "$solutionDir\GameFocus.UI\GameFocus.UI.csproj" -c $Configuration -r $Runtime -o $out --self-contained false -p:PublishTrimmed=false

if ($LASTEXITCODE -ne 0) { Write-Error "Publish failed"; exit $LASTEXITCODE }

Write-Host "Zipping publish folder..."
$zip = "$solutionDir\artifacts\GameFocus-$Runtime.zip"
if (Test-Path $zip) { Remove-Item $zip }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($out, $zip)

Write-Host "Package created: $zip"
Pop-Location
