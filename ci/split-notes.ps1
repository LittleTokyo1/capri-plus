param([string]$Rev = '12f47cb', [switch]$Force)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'docs\notes'
$utf8 = New-Object Text.UTF8Encoding $false
if ((Test-Path (Join-Path $outDir 'Index.md')) -and -not $Force) {
    throw 'docs/notes exists and has been edited since the split; -Force would delete every note and rewrite them from the old files'
}

function Show($path) {
    $psi = New-Object Diagnostics.ProcessStartInfo
    $psi.FileName = 'git'
    $psi.Arguments = "show ${Rev}:$path"
    $psi.WorkingDirectory = $root
    $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $p = [Diagnostics.Process]::Start($psi)
    $text = $p.StandardOutput.ReadToEnd()
    $p.WaitForExit()
    if ($p.ExitCode -ne 0) { throw "git show failed for $path" }
    $text
}

function Lines($path) { @((Show $path) -split "`r?`n") }

function Slug($s, $max) {
    $t = ($s.ToLower() -replace '[^a-z0-9]+', '-').Trim('-')
    if ($t.Length -gt $max) { $t = $t.Substring(0, $max).Trim('-') }
    $t
}

function Sections($lines) {
    $res = @()
    $cur = @{ Head = $null; Body = (New-Object Collections.Generic.List[string]) }
    foreach ($l in $lines) {
        if ($l -match '^## ') {
            $res += ,$cur
            $cur = @{ Head = $l; Body = (New-Object Collections.Generic.List[string]) }
        } else { $cur.Body.Add($l) }
    }
    $res += ,$cur
    $res
}

$notes = @{}
$order = New-Object Collections.Generic.List[string]

function Put($name, $lines) {
    if (-not $notes.ContainsKey($name)) {
        $notes[$name] = New-Object Collections.Generic.List[string]
        $order.Add($name)
    }
    foreach ($l in $lines) { $notes[$name].Add([string]$l) }
}

function Trim-Blank($lines) {
    $a = New-Object Collections.Generic.List[string]
    $a.AddRange([string[]]@($lines))
    while ($a.Count -gt 0 -and $a[$a.Count - 1].Trim() -eq '') { $a.RemoveAt($a.Count - 1) }
    while ($a.Count -gt 0 -and $a[0].Trim() -eq '') { $a.RemoveAt(0) }
    ,$a
}

$sectionNames = @(
    @('Usage rule', 'Usage-rule'),
    @('Feedback batch 2026-10-28 evening', 'Feedback-batch-2026-10-28-evening'),
    @('Feedback 2026-10-28 late', 'Feedback-2026-10-28-late'),
    @('Still open from older batches', 'Still-open-from-older-batches'),
    @("Kasper's answers and follow-up", 'Answers-and-follow-ups-2026-10-29'),
    @('SEELE:', 'SEELE-overview')
)

$frags = @{}
$taskSections = @()

foreach ($sec in (Sections (Lines 'docs/TASKS.md'))) {
    if ($sec.Head -eq $null) {
        Put 'Task-workflow' $sec.Body
        continue
    }
    $secTitle = $sec.Head.Substring(3)
    if ($sec.Head -match '^## CAPRI-(\d{3}[a-z]?)\b') {
        $id = 'CAPRI-' + $matches[1]
        if (-not $frags.ContainsKey($id)) { $frags[$id] = @() }
        $all = New-Object Collections.Generic.List[string]
        $all.Add($sec.Head)
        $all.AddRange([string[]]@($sec.Body))
        $frags[$id] += ,@{ Kind = 'h'; Section = $secTitle; First = $sec.Head; Lines = $all }
        continue
    }
    $name = $null
    foreach ($pair in $sectionNames) { if ($secTitle.StartsWith($pair[0])) { $name = $pair[1] } }
    if (-not $name) { $name = 'Section-' + (Slug $secTitle 50) }
    $taskSections += $name
    $rest = New-Object Collections.Generic.List[string]
    $rest.Add($sec.Head)
    $frag = $null
    foreach ($l in $sec.Body) {
        if ($l -match '^- \*\*CAPRI-(\d{3}[a-z]?)') {
            $id = 'CAPRI-' + $matches[1]
            $frag = @{ Kind = 'b'; Section = $secTitle; First = $l; Lines = (New-Object Collections.Generic.List[string]) }
            $frag.Lines.Add($l)
            if (-not $frags.ContainsKey($id)) { $frags[$id] = @() }
            $frags[$id] += ,$frag
            $rest.Add("- Moved to [[$id]]")
        } elseif ($frag -ne $null -and $l.Length -gt 0 -and $l -notmatch '^\s') {
            $frag = $null
            $rest.Add($l)
        } elseif ($frag -ne $null) {
            $frag.Lines.Add($l)
        } else {
            $rest.Add($l)
        }
    }
    Put $name $rest
}

function TitleStatus($frag) {
    $t = ''
    if ($frag.Kind -eq 'h') {
        $t = $frag.First -replace '^## CAPRI-\d{3}[a-z]?\s*', ''
    } elseif ($frag.First -match '^- \*\*CAPRI-\d{3}[a-z]?\+?\s*(.*?)\*\*') {
        $t = $matches[1]
    }
    $title = $t
    $status = ''
    $i = $t.IndexOf(':')
    if ($i -ge 0) { $title = $t.Substring(0, $i); $status = $t.Substring($i + 1) }
    $title = ($title -replace '\s+', ' ').Trim().TrimEnd('.')
    $status = ($status -replace '\s+', ' ').Trim().TrimEnd('.')
    if ($status.Length -gt 90) { $status = $status.Substring(0, 90).Trim() + '...' }
    @{ Title = $title; Status = $status }
}

$auditsFor = @{}
$auditNames = @()
$auditSections = Sections (Lines 'docs/AUDIT.md')
$n = 0
foreach ($sec in $auditSections) {
    if ($sec.Head -eq $null) { continue }
    $n++
    $head = $sec.Head.Substring(3)
    $name = 'Audit-{0:00}-{1}' -f $n, (Slug $head 45)
    $auditNames += $name
    $rel = @()
    foreach ($m in [regex]::Matches($head, 'CAPRI-\d{3}[a-z]?')) { $rel += $m.Value }
    if ($head -match '1e3ce2c') { $rel += 'CAPRI-021'; $rel += 'CAPRI-013' }
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add($sec.Head)
    if ($rel.Count -gt 0) {
        $links = ($rel | Select-Object -Unique | ForEach-Object { "[[$_]]" }) -join ' '
        $lines.Add('')
        $lines.Add("Related: $links")
        foreach ($r in ($rel | Select-Object -Unique)) {
            if (-not $auditsFor.ContainsKey($r)) { $auditsFor[$r] = @() }
            $auditsFor[$r] += $name
        }
    }
    if ($head -match 'follow-up batch') {
        $lines.Add('')
        $lines.Add('Related: [[Answers-and-follow-ups-2026-10-29]]')
    }
    $lines.AddRange([string[]]@($sec.Body))
    Put $name $lines
}
$auditLog = New-Object Collections.Generic.List[string]
$auditLog.AddRange([string[]]@(($auditSections | Where-Object { $_.Head -eq $null } | Select-Object -First 1).Body))
$auditLog.Add('')
foreach ($a in $auditNames) { $auditLog.Add("- [[$a]]") }
Put 'Audit-log' $auditLog

$taskIds = @($frags.Keys | Sort-Object)
$index = @{}
foreach ($id in $taskIds) {
    $title = ''
    $status = ''
    foreach ($f in $frags[$id]) {
        $ts = TitleStatus $f
        if ($title -eq '' -and $ts.Title -ne '') { $title = $ts.Title }
        if ($ts.Status -ne '') { $status = $ts.Status }
    }
    if ($status -eq '') { $status = 'see note' }
    $index[$id] = @{ Title = $title; Status = $status }
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add("# $id $title")
    $lines.Add('')
    $lines.Add("Status: $status")
    if ($auditsFor.ContainsKey($id)) {
        $lines.Add('')
        $lines.Add('Audit: ' + (($auditsFor[$id] | ForEach-Object { "[[$_]]" }) -join ' '))
    }
    foreach ($f in $frags[$id]) {
        $lines.Add('')
        $lines.Add('*Moved from TASKS.md, section "' + $f.Section + '"*')
        $lines.Add('')
        $body = Trim-Blank $f.Lines
        foreach ($b in $body) { $lines.Add($b) }
    }
    Put $id $lines
}

$arch = Sections (Lines 'docs/ARCHITECTURE.md')
$areas = @(
    @('discord', 'Discord bot', '^(Discord|ForkDiscord|ForkSmokeDiscord)'),
    @('vault-google-heartbeat', 'Vault, Google and heartbeat', '^(Vault|Google|ForkSmokeGoogle|Heartbeat|ForkHeartbeat|ForkSmokeHeartbeat)'),
    @('voice', 'Voice and cloned voice', '^(ForkVoice|Chatterbox)'),
    @('game', 'League, live game and lockout', '^(ForkLeague|ForkLive|League\.cs|ProBuilds|Lockout)'),
    @('hands', 'Hands, monitors, chat reading and the thread', '^(Screens|ForkMonitors|ForkChat\.cs|ForkLookup|Thread)'),
    @('core', 'Core, settings, brain helpers and logging', '^(Fork\.cs|ForkSettings|ForkCompanion|ForkBrain|Quick|Spend|ActivityLog|Flavour)'),
    @('smoke', 'Smoke checks', '^ForkSmoke')
)
$archHead = @{
    'What it is' = 'Arch-overview'
    'Build, test, deploy' = 'Arch-build-test-deploy'
    'Hooks in upstream files' = 'Arch-hooks'
    'Brains' = 'Arch-brains'
    'Safety rules' = 'Arch-safety-rules'
}
$areaRows = @{}
foreach ($a in $areas) { $areaRows[$a[0]] = New-Object Collections.Generic.List[string] }
foreach ($sec in $arch) {
    if ($sec.Head -eq $null) { Put 'Arch-overview' $sec.Body; continue }
    $title = $sec.Head.Substring(3)
    if ($title -eq 'Our files') {
        foreach ($l in $sec.Body) {
            if ($l -match '^\|(\s*File\s*\||---)' -or $l.Trim() -eq '') { continue }
            $cell = $l -replace '^\|\s*', ''
            $hit = $null
            foreach ($a in $areas) { if (-not $hit -and $cell -match $a[2]) { $hit = $a[0] } }
            if (-not $hit) { throw "no area for table row: $l" }
            $areaRows[$hit].Add($l)
        }
        continue
    }
    $name = $null
    foreach ($k in $archHead.Keys) { if ($title.StartsWith($k)) { $name = $archHead[$k] } }
    if (-not $name) { throw "no note for architecture section: $title" }
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add($sec.Head)
    $lines.AddRange([string[]]@($sec.Body))
    Put $name $lines
}
$filesHub = New-Object Collections.Generic.List[string]
$filesHub.Add('## Our files')
$filesHub.Add('')
foreach ($a in $areas) {
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add('# Code area: ' + $a[1])
    $lines.Add('')
    $lines.Add('Part of [[Arch-files]]. Build and deploy are in [[Arch-build-test-deploy]].')
    $lines.Add('')
    $lines.Add('| File | Role |')
    $lines.Add('|---|---|')
    $lines.AddRange([string[]]@($areaRows[$a[0]]))
    Put ('Arch-files-' + $a[0]) $lines
    $filesHub.Add('- [[Arch-files-' + $a[0] + ']] ' + $a[1])
}
Put 'Arch-files' $filesHub

$hand = Sections (Lines 'docs/HANDOFF.md')
$handNames = @()
$n = 0
foreach ($sec in $hand) {
    if ($sec.Head -eq $null) { continue }
    $n++
    $name = 'Handoff-{0:00}-{1}' -f $n, (Slug $sec.Head.Substring(3) 45)
    $handNames += $name
    $lines = New-Object Collections.Generic.List[string]
    $lines.Add($sec.Head)
    $lines.AddRange([string[]]@($sec.Body))
    Put $name $lines
}
$handLog = New-Object Collections.Generic.List[string]
$handLog.AddRange([string[]]@(($hand | Where-Object { $_.Head -eq $null } | Select-Object -First 1).Body))
$handLog.Add('')
foreach ($h in $handNames) { $handLog.Add("- [[$h]]") }
Put 'Handoff-log' $handLog

New-Item -ItemType Directory -Force $outDir | Out-Null
foreach ($f in (Get-ChildItem $outDir -Filter *.md -File)) { Remove-Item $f.FullName }
$all = New-Object Collections.Generic.List[string]
foreach ($name in $order) {
    $text = ((Trim-Blank $notes[$name]) -join "`n") + "`n"
    [IO.File]::WriteAllText((Join-Path $outDir ($name + '.md')), $text, $utf8)
    $all.Add($name)
}

$ix = New-Object Collections.Generic.List[string]
$ix.Add('# Capri build notes: Index')
$ix.Add('')
$ix.Add('Read this page and `docs/SESSION_CHECKPOINT.md` at the start of a session, then open only the notes the task touches. The notes moved out of TASKS.md, ARCHITECTURE.md, HANDOFF.md and AUDIT.md on CAPRI-039 (text kept as it was; those four files are now one-line stubs). Rules and the next action are in `CLAUDE.md` and the checkpoint.')
$ix.Add('')
$ix.Add('## How to keep it')
$ix.Add('- A finished task: update its `CAPRI-0xx` note (evidence, "Kasper must verify") and its status line below; add a new task as a new note named by its ID and list it here.')
$ix.Add('- Code structure changed: edit the matching `Arch-files-*` note. A review finding: a new `Audit-NN-*` note, listed in [[Audit-log]], with a `Related:` line. A session summary: a new `Handoff-NN-*` note, listed in [[Handoff-log]].')
$ix.Add('- Every note must be reachable from this page. Check with `./ci/check-notes.ps1`; fix anything it lists before committing.')
$ix.Add('- Not in these notes: `docs/PLAN.md` (the build plan, source of truth for order) and `docs/SESSION_CHECKPOINT.md` (where we are and the exact next action).')
$ix.Add('')
$ix.Add('## Workflow and standing notes')
$ix.Add('- [[Task-workflow]] the per-task workflow and status words')
foreach ($s in $taskSections) { $ix.Add("- [[$s]]") }
$ix.Add('')
$ix.Add('## Code areas')
$ix.Add('- [[Arch-overview]] what the app is and how we layer on upstream')
$ix.Add('- [[Arch-build-test-deploy]] build, smoke test, deploy routine, user data')
$ix.Add('- [[Arch-files]] our files by area: ' + ((($areas | ForEach-Object { '[[Arch-files-' + $_[0] + ']]' })) -join ', '))
$ix.Add('- [[Arch-hooks]] hooks in upstream files')
$ix.Add('- [[Arch-brains]] which model does what')
$ix.Add('- [[Arch-safety-rules]] lockout, spend cap, logging, League rules')
$ix.Add('')
$ix.Add('## History')
$ix.Add('- [[Audit-log]] review findings, one note per review')
$ix.Add('- [[Handoff-log]] state summaries by date')
$ix.Add('')
$ix.Add('## Tasks')
foreach ($id in $taskIds) {
    $ix.Add("- [[$id]] " + $index[$id].Title + ': ' + $index[$id].Status)
}
[IO.File]::WriteAllText((Join-Path $outDir 'Index.md'), (($ix -join "`n") + "`n"), $utf8)

Write-Host ('notes written: {0} (plus Index.md)' -f $all.Count)
Write-Host ('tasks: {0}' -f $taskIds.Count)
