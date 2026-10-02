param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory
)

# Installs staging/phase1 into the game directory. Refuses to run while GTA IV is open, checks that
# the files it was built from are unchanged, backs up every file it replaces into
# scripts\LibertyFramework\backups\phase1-<timestamp>\ with a rollback manifest, then verifies hashes.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$stage = Join-Path $repoRoot 'staging\phase1'
$manifest = Get-Content -LiteralPath (Join-Path $stage 'manifest.json') -Raw | ConvertFrom-Json

if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it before installing.' }
if ((Get-Item -LiteralPath (Join-Path $game 'GTAIV.exe')).VersionInfo.FileVersion -ne $manifest.gameVersion) { throw "GTAIV.exe is not $($manifest.gameVersion)." }
foreach ($required in @('ScriptHookDotNet.asi', 'dinput8.dll', 'plugins\GTAIV.EFLC.FusionFix.asi')) {
    if (-not (Test-Path -LiteralPath (Join-Path $game $required))) { throw "Missing dependency: $required" }
}
if ((Get-Content -LiteralPath (Join-Path $game 'plugins\GTAIV.EFLC.FusionFix.ini') -Raw) -notmatch '(?m)^\s*ExtendedLimits\s*=\s*1(?:\s|$)') {
    throw 'FusionFix ExtendedLimits=1 is required.'
}
if ((Get-FileHash -LiteralPath (Join-Path $game 'update\common\data\WeaponInfo.xml') -Algorithm SHA256).Hash -ne $manifest.baseWeaponInfoSha256) {
    throw 'Installed WeaponInfo.xml changed since the package was built. Re-run tools\package-phase1.ps1.'
}
if ((Get-FileHash -LiteralPath (Join-Path $game 'update\common\data\default.dat') -Algorithm SHA256).Hash -ne $manifest.baseDefaultDatSha256) {
    throw 'Installed update\common\data\default.dat changed since the package was built. Re-run tools\package-phase1.ps1.'
}
foreach ($file in $manifest.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $stage $file.path) -Algorithm SHA256).Hash -ne $file.sha256) { throw "Staged file changed after packaging: $($file.path)" }
}

$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss')
$backupRoot = Join-Path $game "scripts\LibertyFramework\backups\phase1-$stamp"
New-Item -ItemType Directory -Force -Path $backupRoot | Out-Null
$actions = @()
foreach ($file in $manifest.files) {
    $target = Join-Path $game $file.path
    $exists = Test-Path -LiteralPath $target
    if ($exists -and $file.policy -eq 'keep-existing') {
        $actions += [ordered]@{ path = $file.path; action = 'kept'; backup = $null }
        continue
    }
    $backup = $null
    if ($exists) {
        $backup = Join-Path 'files' $file.path
        $backupPath = Join-Path $backupRoot $backup
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backupPath) | Out-Null
        Copy-Item -LiteralPath $target -Destination $backupPath
    }
    $actions += [ordered]@{ path = $file.path; action = $(if ($exists) { 'replaced' } else { 'created' }); backup = $backup; sha256 = $file.sha256 }
}
$rollback = [ordered]@{ package = $manifest.package; installedUtc = (Get-Date).ToUniversalTime().ToString('o'); actions = $actions }
[IO.File]::WriteAllText((Join-Path $backupRoot 'rollback.json'), ($rollback | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
Write-Host "Backup written: $backupRoot"

try {
    foreach ($action in $actions) {
        if ($action.action -eq 'kept') { continue }
        $target = Join-Path $game $action.path
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath (Join-Path $stage $action.path) -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $action.sha256) { throw "Hash mismatch after copy: $($action.path)" }
        Write-Host ("  {0,-9} {1}" -f $action.action, $action.path)
    }
    Set-Content -LiteralPath (Join-Path $game 'scripts\LibertyFramework\t007-weaponinfo.sha256') -NoNewline `
        -Value (Get-FileHash -LiteralPath (Join-Path $game 'update\common\data\WeaponInfo.xml') -Algorithm SHA256).Hash
}
catch {
    Write-Host "Install failed: $_  Rolling back..."
    & (Join-Path $PSScriptRoot 'rollback-phase1.ps1') -GameDirectory $game -BackupDirectory $backupRoot
    throw
}
Write-Host 'Phase 1 installed and verified. Rollback: tools\rollback-phase1.ps1 -GameDirectory <GTAIV folder>'
