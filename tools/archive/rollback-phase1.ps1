param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory,
    [string] $BackupDirectory
)

# Undoes a Phase 1 install using its rollback.json: restores every replaced file from the backup and
# deletes files the install created. Uses the newest phase1-* backup when none is given.
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it before rolling back.' }
if (-not $BackupDirectory) {
    $latest = Get-ChildItem -LiteralPath (Join-Path $game 'scripts\LibertyFramework\backups') -Directory -Filter 'phase1-*' |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($null -eq $latest) { throw 'No phase1 backup found.' }
    $BackupDirectory = $latest.FullName
}
$rollback = Get-Content -LiteralPath (Join-Path $BackupDirectory 'rollback.json') -Raw | ConvertFrom-Json
foreach ($action in $rollback.actions) {
    $target = Join-Path $game $action.path
    if ($action.action -eq 'replaced') {
        Copy-Item -LiteralPath (Join-Path $BackupDirectory $action.backup) -Destination $target -Force
        Write-Host "  restored $($action.path)"
    }
    elseif ($action.action -eq 'created' -and (Test-Path -LiteralPath $target)) {
        Remove-Item -LiteralPath $target -Force
        Write-Host "  removed  $($action.path)"
    }
}
$weaponInfo = Join-Path $game 'update\common\data\WeaponInfo.xml'
if (Test-Path -LiteralPath $weaponInfo) {
    Set-Content -LiteralPath (Join-Path $game 'scripts\LibertyFramework\t007-weaponinfo.sha256') -NoNewline `
        -Value (Get-FileHash -LiteralPath $weaponInfo -Algorithm SHA256).Hash
}
Write-Host "Rolled back using $BackupDirectory"
