param([string]$Exe, [int]$Seconds = 300)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $root 'out\windows\Capri.exe' }
if (-not (Test-Path -LiteralPath $Exe)) { throw "Capri.exe not found at $Exe. Build first with ./ci/build-windows.ps1." }
$folder = Join-Path $env:TEMP ('capri-memory-eval-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$run = Start-Process -FilePath $Exe -ArgumentList @('--memory-eval', ('"' + $folder + '"')) -PassThru -WindowStyle Hidden
if (-not $run.WaitForExit($Seconds * 1000)) { $run.Kill(); throw 'The memory eval timed out.' }
$report = Join-Path $folder 'memory-eval.txt'
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report } else { throw "No report was written in $folder." }
if ($run.ExitCode -ne 0) { Write-Output "Exit code $($run.ExitCode). Temp vaults and the activity log are in $folder." }
