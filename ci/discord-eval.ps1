param([string]$Exe, [int]$Seconds = 900, [string]$Only = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) { $Exe = Join-Path $root 'out\windows\Capri.exe' }
if (-not (Test-Path -LiteralPath $Exe)) { throw "Capri.exe not found at $Exe. Build first with ./ci/build-windows.ps1." }
$folder = Join-Path $env:TEMP ('capri-discord-eval-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$list = @('--discord-eval', ('"' + $folder + '"'))
if ($Only) { $list += $Only }
$run = Start-Process -FilePath $Exe -ArgumentList $list -PassThru -WindowStyle Hidden
if (-not $run.WaitForExit($Seconds * 1000)) { $run.Kill(); throw 'The Discord eval timed out.' }
$report = Join-Path $folder 'discord-eval.txt'
if (Test-Path -LiteralPath $report) { Get-Content -LiteralPath $report } else { throw "No report was written in $folder." }
if ($run.ExitCode -ne 0) { Write-Output "Exit code $($run.ExitCode). Temp vaults, errors and the activity log are in $folder." }
