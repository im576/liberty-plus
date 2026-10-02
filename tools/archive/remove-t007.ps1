param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$target = Join-Path $game 'update\common\data\WeaponInfo.xml'
$receipt = Join-Path $game 'scripts\LibertyFramework\t007-weaponinfo.sha256'
if (Get-Process GTAIV -ErrorAction SilentlyContinue) {
    throw 'GTA IV is running. Close it before removing T-007 data.'
}
if (-not (Test-Path -LiteralPath $receipt)) { throw 'T-007 receipt is missing; no files were removed.' }
if (-not (Test-Path -LiteralPath $target)) { throw 'T-007 data is missing; no files were removed.' }
if ((Get-Content -LiteralPath $receipt -Raw).Trim() -ne
    (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
    throw 'T-007 data changed after installation; no files were removed.'
}
Remove-Item -LiteralPath $target
Remove-Item -LiteralPath $receipt
Write-Host 'Removed only the T-007 data override and receipt.'
