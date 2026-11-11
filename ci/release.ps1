param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "",
    [switch]$Publish,
    [int]$SmokeSeconds = 900
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$repo = 'LittleTokyo1/capri-plus'
$info = Get-Content (Join-Path $root 'app\AssemblyInfo.cs') -Raw
if ($info -notmatch ('AssemblyVersion\("' + [regex]::Escape($Version) + '"\)')) { throw "Set AssemblyVersion(""$Version"") and AssemblyFileVersion(""$Version"") in app\AssemblyInfo.cs first." }
if ($Publish -and $Notes.Trim().Length -eq 0) { throw "Give -Notes for the public release page (plain words, nothing private)." }
$out = Join-Path $root 'out\release'
$started = Get-Date
& (Join-Path $PSScriptRoot 'build-windows.ps1') -SmokeTest -SmokeSeconds $SmokeSeconds -OutputDirectory $out
$setup = Join-Path $out 'Capri-Setup.exe'
if (-not (Test-Path $setup) -or (Get-Item $setup).LastWriteTime -lt $started) { throw "The smoke build made no new Capri-Setup.exe." }
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash.ToLowerInvariant()
$sums = Join-Path $out 'SHA256SUMS.txt'
[System.IO.File]::WriteAllText($sums, "$hash  Capri-Setup.exe`n", (New-Object System.Text.UTF8Encoding($false)))
$license = Join-Path $out 'LICENSE.md'
Copy-Item (Join-Path $root 'LICENSE.md') $license -Force
$words = Join-Path $env:APPDATA 'Capri-private\private-words.txt'
if (-not (Test-Path $words)) { throw "The private word list $words is missing, so this release can't be checked for personal data. Nothing was published." }
$terms = @(Get-Content $words | ForEach-Object { $_.Trim() } | Where-Object { $_.Length -ge 4 -and -not $_.StartsWith('#') -and $repo.IndexOf($_, [StringComparison]::OrdinalIgnoreCase) -lt 0 })
if ($terms.Count -eq 0) { throw "The private word list is empty, so this release can't be checked. Nothing was published." }
$latin = [System.Text.Encoding]::GetEncoding(28591)
$hits = 0
foreach ($file in @((Join-Path $out 'Capri.exe'), $setup, $sums, $license)) {
    $bytes = [System.IO.File]::ReadAllBytes($file)
    $plain = $latin.GetString($bytes)
    $wide = [System.Text.Encoding]::Unicode.GetString($bytes)
    $odd = if ($bytes.Length -gt 1) { [System.Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1) } else { '' }
    for ($i = 0; $i -lt $terms.Count; $i++) {
        $term = $terms[$i]
        if ($plain.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $wide.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $odd.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hits++; "Private word #$($i + 1) found in " + (Split-Path $file -Leaf) }
    }
}
for ($i = 0; $i -lt $terms.Count; $i++) { if ($Notes.IndexOf($terms[$i], [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hits++; "Private word #$($i + 1) found in the release notes" } }
if ($hits -gt 0) { throw "$hits private word hit(s) in this release (#N counts the words of 4 or more letters in $words). Nothing was published." }
"Privacy check: none of the $($terms.Count) private words in Capri.exe, Capri-Setup.exe, SHA256SUMS.txt, LICENSE.md or the notes."
if (-not $Publish) {
    "Release v$Version is ready in $out (Capri-Setup.exe, SHA256SUMS.txt, LICENSE.md). Run again with -Publish -Notes ""..."" to put it on $repo."
    return
}
$notesFile = Join-Path $out 'release-notes.md'
[System.IO.File]::WriteAllText($notesFile, $Notes, (New-Object System.Text.UTF8Encoding($false)))
gh release create "v$Version" $setup $sums $license --repo $repo --title "Capri+ $Version" --notes-file $notesFile
if ($LASTEXITCODE -ne 0) { throw "gh release create failed." }
"Published v$Version on https://github.com/$repo/releases"
& (Join-Path $PSScriptRoot 'sync-public.ps1')
