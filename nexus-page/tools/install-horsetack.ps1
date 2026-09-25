# Install a HorseTack release zip (with the Nexus UpdateKey) on Ben's PC.
# Run AFTER rebuild_with_updatekey.sh on the box and CopyFromBox of the zip to C:\Users\Glim\codex-stage.
#   powershell -ExecutionPolicy Bypass -File E:\Codex-Mods\stardew\HorseTack\nexus-page\tools\install-horsetack.ps1 -NexusId <id> -Sha256 <hash from the box> [-Version 1.4.1]
# Steps: refuse while Stardew/SMAPI is running -> verify staged zip hash + manifest (Version, UpdateKeys Nexus:<id>)
# -> copy to E:\...\HorseTack\release -> E: clone manifest (git pull --ff-only, else patch UpdateKeys in place)
# -> reinstall Mods\HorseTack keeping config.json byte-for-byte -> hash-check every installed file.
# Written for Windows PowerShell 5.1 (no Get-Content -Raw).
param(
  [Parameter(Mandatory = $true)][string]$NexusId,
  [Parameter(Mandatory = $true)][string]$Sha256,
  [string]$Version = '1.4.1',
  [string]$StageZip = '',
  [string]$RepoDir = 'E:\Codex-Mods\stardew\HorseTack',
  [string]$ModsDir = 'C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods',
  [string]$ConfigBackup = 'C:\Users\Glim\codex-stage\horsetack-config-backup.json',
  [switch]$SkipGit
)
Add-Type -AssemblyName System.IO.Compression.FileSystem
$fail = $false
function Check($cond, $msg) { if ($cond) { Write-Output "PASS $msg" } else { Write-Output "FAIL $msg"; $script:fail = $true } }
function FileSha($p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower() }
function StreamSha($s) { $h = [Security.Cryptography.SHA256]::Create(); try { ([BitConverter]::ToString($h.ComputeHash($s)) -replace '-', '').ToLower() } finally { $h.Dispose() } }

if ($NexusId -notmatch '^\d+$') { Write-Output "FAIL NexusId must be a number"; exit 2 }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Write-Output "FAIL Version must look like 1.4.1"; exit 2 }
if (-not $StageZip) { $StageZip = "C:\Users\Glim\codex-stage\HorseTack-$Version.zip" }
$key = "Nexus:$NexusId"
$verRx = '"Version"\s*:\s*"' + [regex]::Escape($Version) + '"'
$running = Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^(StardewModdingAPI|Stardew Valley)$' }
if ($running) { Write-Output "FAIL Stardew/SMAPI is running (PID $($running.Id -join ', ')); close it first (nothing changed)"; exit 1 }

# 1. staged zip
if (-not (Test-Path -LiteralPath $StageZip)) { Write-Output "FAIL missing $StageZip (CopyFromBox it first)"; exit 1 }
$zipSha = FileSha $StageZip
Check ($zipSha -eq $Sha256.ToLower()) "staged zip sha256 $zipSha matches the box"
$z = [IO.Compression.ZipFile]::OpenRead($StageZip)
try {
  $m = $z.GetEntry('HorseTack/manifest.json')
  $r = New-Object IO.StreamReader($m.Open()); $mtext = $r.ReadToEnd(); $r.Close()
  Check ($mtext -match $verRx) "zip manifest Version $Version"
  Check ($mtext -match ('"UpdateKeys"\s*:\s*\[\s*"' + [regex]::Escape($key) + '"\s*\]')) "zip manifest UpdateKeys [$key]"
  Check (-not ($z.Entries | Where-Object { $_.FullName -match 'config\.json$|deps\.json$' })) 'zip has no config.json / deps.json'
} finally { $z.Dispose() }
if ($fail) { Write-Output 'RESULT: FAIL (nothing changed)'; exit 1 }

# 2. E:\...\release
$rel = Join-Path $RepoDir 'release'
if (-not (Test-Path -LiteralPath $rel)) { New-Item -ItemType Directory -Path $rel | Out-Null }
$relZip = Join-Path $rel "HorseTack-$Version.zip"
Copy-Item -LiteralPath $StageZip -Destination $relZip -Force
Check ((FileSha $relZip) -eq $zipSha) "E: release copy $relZip"

# 3. E: clone manifest
$man = Join-Path $RepoDir 'manifest.json'
if (-not $SkipGit) {
  $out = cmd /c "git -C `"$RepoDir`" pull --ff-only 2>&1"
  Write-Output "git pull: $($out -join ' | ')"
}
$mt = [IO.File]::ReadAllText($man)
if ($mt -notmatch [regex]::Escape($key)) {
  $mt2 = [regex]::Replace($mt, '"UpdateKeys"\s*:\s*\[[^\]]*\]', ('"UpdateKeys": [ "' + $key + '" ]'))
  [IO.File]::WriteAllText($man, $mt2, (New-Object Text.UTF8Encoding($false)))
  Write-Output 'E: manifest patched in place (not committed; box commit + pull keeps them in sync)'
}
$mt = [IO.File]::ReadAllText($man)
Check (($mt -match [regex]::Escape($key)) -and ($mt -match $verRx)) "E: clone manifest has $key and $Version"

# 4. reinstall into Mods, keeping config.json
$dest = Join-Path $ModsDir 'HorseTack'
$cfg = Join-Path $dest 'config.json'
$cfgBytes = $null
if (Test-Path -LiteralPath $cfg) {
  $cfgBytes = [IO.File]::ReadAllBytes($cfg)
  [IO.File]::WriteAllBytes($ConfigBackup, $cfgBytes)
}
if (Test-Path -LiteralPath $dest) { Remove-Item -LiteralPath $dest -Recurse -Force }
[IO.Compression.ZipFile]::ExtractToDirectory($StageZip, $ModsDir)
if ($cfgBytes -ne $null) { [IO.File]::WriteAllBytes($cfg, $cfgBytes) }

# 5. hash check installed files against the zip
$z = [IO.Compression.ZipFile]::OpenRead($StageZip)
$bad = 0; $n = 0
try {
  foreach ($e in $z.Entries) {
    if ($e.FullName.EndsWith('/')) { continue }
    $n++
    $p = Join-Path $ModsDir ($e.FullName -replace '/', '\')
    $s = $e.Open(); $zs = StreamSha $s; $s.Close()
    if (-not (Test-Path -LiteralPath $p) -or (FileSha $p) -ne $zs) { $bad++; Write-Output "  mismatch: $($e.FullName)" }
  }
} finally { $z.Dispose() }
Check ($bad -eq 0) "installed $n files match the zip byte for byte"
if ($cfgBytes -ne $null) { Check ((FileSha $cfg) -eq (FileSha $ConfigBackup)) 'config.json kept byte-for-byte' }
$im = [IO.File]::ReadAllText((Join-Path $dest 'manifest.json'))
Check (($im -match [regex]::Escape($key)) -and ($im -match $verRx)) "installed manifest has $key and $Version"
if ($fail) { Write-Output 'RESULT: FAIL'; exit 1 } else { Write-Output "RESULT: PASS  (zip sha256 $zipSha)" }
