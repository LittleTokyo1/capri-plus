param([string]$Message = "")
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = 'LittleTokyo1/capri-plus'
$public = @('app', 'assets', 'ci', 'site', 'worker', '.github', '.gitignore', 'SECURITY.md', 'README.md', 'LICENSE.md', 'CONTRIBUTING.md', 'docs/system-map')
$words = Join-Path $env:APPDATA 'Capri-private\private-words.txt'
if (-not (Test-Path $words)) { throw "The private word list $words is missing, so the public code can't be checked. Nothing was pushed." }
$terms = @(Get-Content $words | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -ge 4 -and -not $_.StartsWith('#') -and $repo.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 })
if ($terms.Count -eq 0) { throw "The private word list is empty, so the public code can't be checked. Nothing was pushed." }
$work = Join-Path $env:TEMP ('capri-plus-sync-' + [guid]::NewGuid().ToString('N'))
git clone -q "https://github.com/$repo.git" $work
if ($LASTEXITCODE -ne 0) { throw "Could not clone $repo." }
Get-ChildItem $work -Force | Where-Object { $_.Name -notin '.git', 'presence' } | Remove-Item -Recurse -Force
$tar = Join-Path $env:TEMP ('capri-public-' + [guid]::NewGuid().ToString('N') + '.tar')
git -C $root archive --format=tar -o $tar HEAD $public
if ($LASTEXITCODE -ne 0) { throw 'git archive failed.' }
tar -xf $tar -C $work
Remove-Item $tar -Force
$hits = 0
foreach ($file in (Get-ChildItem $work -Recurse -File | Where-Object { $_.FullName -notmatch '\\\.git\\' })) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    for ($i = 0; $i -lt $terms.Count; $i++) { if ($text.IndexOf($terms[$i], [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hits++; "Private word #$($i + 1) in " + $file.FullName.Substring($work.Length + 1) } }
}
if ($hits -gt 0) { Remove-Item $work -Recurse -Force; throw "$hits private word hit(s) in the public code. Nothing was pushed." }
foreach ($kept in @('CLAUDE.md', 'CLAUDE.local.md', 'bridge', '.claude', 'docs\notes', 'docs\SESSION_CHECKPOINT.md', 'docs\PLAN.md')) { if (Test-Path (Join-Path $work $kept)) { Remove-Item $work -Recurse -Force; throw "$kept would have gone public. Nothing was pushed." } }
git -C $work add -A
$changed = git -C $work status --porcelain
if (-not $changed) { Remove-Item $work -Recurse -Force; "The public code on $repo is already up to date."; return }
$version = ([regex]::Match((Get-Content (Join-Path $root 'app\AssemblyInfo.cs') -Raw), 'AssemblyVersion\("([^"]+)"\)')).Groups[1].Value
$subject = if ($Message.Trim().Length -gt 0) { $Message.Trim() } else { "Capri+ $version" }
git -C $work commit -q -m $subject
git -C $work push -q origin main
if ($LASTEXITCODE -ne 0) { throw "Pushing the public code to $repo failed." }
Remove-Item $work -Recurse -Force
"Public code on $repo synced ($subject); privacy check: none of the $($terms.Count) private words."
