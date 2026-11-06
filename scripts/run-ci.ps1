<#
Runs the full verification pipeline locally:
- restore
- build
- list vulnerable/outdated packages
- run tests (unit + integration)
- collect coverage (XPlat)
- generate report (markdown + optional HTML if reportgenerator is available)

Usage: powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-ci.ps1
#>
Set-StrictMode -Version Latest
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Push-Location $root

function Run-Step($name, [ScriptBlock]$action) {
	Write-Host "=== $name ==="
	$sw = [System.Diagnostics.Stopwatch]::StartNew()
	$ok = $false
	try {
		& $action
		$ok = $LASTEXITCODE -eq 0
	}
	catch {
		$ok = $false
		Write-Host "ERROR: $_" -ForegroundColor Red
	}
	$sw.Stop()
	return [PSCustomObject]@{ Name = $name; Success = $ok; ElapsedMs = $sw.ElapsedMilliseconds }
}

$results = @()

# 1) Restore
$results += Run-Step 'dotnet restore' { dotnet restore }

# 2) Build
$results += Run-Step 'dotnet build' { dotnet build -c Release }

# 3) List vulnerable packages
Write-Host "Checking vulnerable packages..."
try { dotnet list package --vulnerable > .\artifacts\vulnerable-packages.txt } catch { }

# 4) List outdated packages
try { dotnet list package --outdated > .\artifacts\outdated-packages.txt } catch { }

# Ensure artifacts dir
New-Item -ItemType Directory -Path .\artifacts -Force | Out-Null

# 5) Run tests with coverage (XPlat collector)
$testSw = [System.Diagnostics.Stopwatch]::StartNew()
Write-Host "Running tests with coverage collection..."
try {
	$sln = Get-ChildItem -Path $root -Filter *.sln* | Select-Object -First 1
	if ($null -ne $sln) {
		dotnet test $sln.FullName -c Release --logger "trx;LogFileName=TestResults.trx" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./artifacts/coverage/
	} else {
		dotnet test -c Release --logger "trx;LogFileName=TestResults.trx" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:CoverletOutput=./artifacts/coverage/
	}
	$testExit = $LASTEXITCODE
} catch {
	$testExit = 1
}
$testSw.Stop()
$results += [PSCustomObject]@{ Name = 'dotnet test (with coverage)'; Success = ($testExit -eq 0); ElapsedMs = $testSw.ElapsedMilliseconds }

# 6) Generate HTML coverage report if reportgenerator available
$rg = Get-Command reportgenerator -ErrorAction SilentlyContinue
if ($null -ne $rg) {
	Write-Host "Generating HTML coverage report..."
	try {
		& reportgenerator -reports:./artifacts/coverage/*.cobertura.xml -targetdir:./artifacts/coverage-report -reporttypes:Html
	} catch {
		Write-Host "Report generation failed: $_" -ForegroundColor Yellow
	}
} else {
	Write-Host "reportgenerator not found; skipping HTML coverage report (install dotnet tool: dotnet tool install -g dotnet-reportgenerator-globaltool)" -ForegroundColor Yellow
}

# 7) Basic summary
$report = "# CI Run Report`n`n"
$report += "Date: $(Get-Date -Format s)`n`n"
$report += "## Steps`n`
"
foreach ($r in $results) {
	$status = if ($r.Success) { '✔' } else { '✖' }
	$report += "- $($r.Name): $status ($([int]$r.ElapsedMs) ms)`n"
}
$report += "`n"

# Vulnerable / outdated summaries
if (Test-Path .\artifacts\vulnerable-packages.txt) {
	$v = Get-Content .\artifacts\vulnerable-packages.txt | Select-Object -First 200
	$report += "## Vulnerable packages (sample)`n```
$( $v -join "`n" )
``` `n
}
if (Test-Path .\artifacts\outdated-packages.txt) {
	$o = Get-Content .\artifacts\outdated-packages.txt | Select-Object -First 200
	$report += "## Outdated packages (sample)`n```
$( $o -join "`n" )
``` `n
}

# Coverage file existence
if (Test-Path .\artifacts\coverage) {
	$covFiles = Get-ChildItem .\artifacts\coverage -Filter *.cobertura.xml -Recurse | Select-Object -ExpandProperty FullName
	if ($covFiles) {
		$report += "Coverage produced: `n"
		foreach ($f in $covFiles) { $report += "- $f`n" }
	}
}

# Test results TRX
if (Test-Path TestResults.trx) { $report += "Test results TRX: TestResults.trx`n" }

# Execution time
$totalMs = ($results | Measure-Object -Property ElapsedMs -Sum).Sum
$report += "`nTotal time (ms): $totalMs`n"

# Write report
New-Item -ItemType Directory -Path .\artifacts -Force | Out-Null
$reportPath = Join-Path .\artifacts "ci-report.md"
Set-Content -Path $reportPath -Value $report -Encoding UTF8

Write-Host "CI run complete. Report written to $reportPath"
Pop-Location
