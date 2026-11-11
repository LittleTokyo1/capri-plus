param([switch]$Lossless, [string]$Rev = '12f47cb')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$dir = Join-Path $root 'docs\notes'
$utf8 = [Text.Encoding]::UTF8
$failed = $false

$files = @(Get-ChildItem $dir -Filter *.md -File)
$names = @{}
foreach ($f in $files) { $names[$f.BaseName] = $f.FullName }

function Links($path) {
    $text = [regex]::Replace([IO.File]::ReadAllText($path, $utf8), '`[^`]*`', '')
    $found = @()
    foreach ($m in [regex]::Matches($text, '\[\[([^\]\|#]+)')) { $found += $m.Groups[1].Value.Trim() }
    $found
}

$broken = @()
foreach ($f in $files) {
    foreach ($l in (Links $f.FullName)) {
        if (-not $names.ContainsKey($l)) { $broken += ('{0} -> [[{1}]]' -f $f.Name, $l) }
    }
}

if (-not $names.ContainsKey('Index')) { throw 'docs/notes/Index.md is missing' }
$seen = @{ 'Index' = $true }
$queue = New-Object Collections.Generic.Queue[string]
$queue.Enqueue('Index')
while ($queue.Count -gt 0) {
    $cur = $queue.Dequeue()
    foreach ($l in (Links $names[$cur])) {
        if ($names.ContainsKey($l) -and -not $seen.ContainsKey($l)) {
            $seen[$l] = $true
            $queue.Enqueue($l)
        }
    }
}
$orphans = @($names.Keys | Where-Object { -not $seen.ContainsKey($_) } | Sort-Object)

Write-Host ('notes: {0}' -f $files.Count)
if ($broken.Count -gt 0) {
    $failed = $true
    Write-Host ('FAIL broken links: {0}' -f $broken.Count)
    $broken | ForEach-Object { Write-Host ('  ' + $_) }
} else { Write-Host 'PASS every [[link]] resolves to a note' }
if ($orphans.Count -gt 0) {
    $failed = $true
    Write-Host ('FAIL notes not reachable from Index.md: {0}' -f $orphans.Count)
    $orphans | ForEach-Object { Write-Host ('  ' + $_) }
} else { Write-Host 'PASS every note is reachable from Index.md' }

if ($Lossless) {
    $have = New-Object Collections.Generic.HashSet[string]
    foreach ($f in $files) {
        foreach ($l in ([IO.File]::ReadAllText($f.FullName, $utf8) -split "`r?`n")) { [void]$have.Add($l.Trim()) }
    }
    foreach ($doc in @('TASKS', 'ARCHITECTURE', 'HANDOFF', 'AUDIT')) {
        $psi = New-Object Diagnostics.ProcessStartInfo
        $psi.FileName = 'git'
        $psi.Arguments = "show ${Rev}:docs/$doc.md"
        $psi.WorkingDirectory = $root
        $psi.RedirectStandardOutput = $true
        $psi.UseShellExecute = $false
        $psi.StandardOutputEncoding = $utf8
        $p = [Diagnostics.Process]::Start($psi)
        $text = $p.StandardOutput.ReadToEnd()
        $p.WaitForExit()
        if ($p.ExitCode -ne 0) { throw "git show failed for $doc.md" }
        $total = 0
        $missing = @()
        foreach ($l in ($text -split "`r?`n")) {
            $t = $l.Trim()
            if ($t -eq '') { continue }
            $total++
            if (-not $have.Contains($t)) { $missing += $t }
        }
        if ($missing.Count -gt 0) {
            $failed = $true
            Write-Host ('FAIL {0}.md: {1} of {2} lines missing from the notes' -f $doc, $missing.Count, $total)
            $missing | Select-Object -First 5 | ForEach-Object { Write-Host ('  ' + $_.Substring(0, [Math]::Min(100, $_.Length))) }
        } else {
            Write-Host ('PASS {0}.md: all {1} non-empty lines are in the notes' -f $doc, $total)
        }
    }
}

if ($failed) { exit 1 }
