param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $game 'GTAIV.exe'
$base = Join-Path $game 'common\data\WeaponInfo.xml'
$fusionConfig = Join-Path $game 'plugins\GTAIV.EFLC.FusionFix.ini'
$source = Join-Path $repoRoot 'staging\t007\WeaponInfo.xml'
$baseHashFile = Join-Path $repoRoot 'staging\t007\base.sha256'
$target = Join-Path $game 'update\common\data\WeaponInfo.xml'
$receipt = Join-Path $game 'scripts\LibertyFramework\t007-weaponinfo.sha256'

if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.2.0.59') {
    throw 'T-007 is prepared only for GTAIV.exe 1.2.0.59.'
}
if (Get-Process GTAIV -ErrorAction SilentlyContinue) {
    throw 'GTA IV is running. Close it before installing T-007 data.'
}
if (-not (Test-Path -LiteralPath $fusionConfig) -or
    (Get-Content -LiteralPath $fusionConfig -Raw) -notmatch '(?m)^\s*ExtendedLimits\s*=\s*1(?:\s|$)') {
    throw 'FusionFix ExtendedLimits=1 is required for custom weapon registration.'
}
if ((Get-Content -LiteralPath $baseHashFile -Raw).Trim() -ne
    (Get-FileHash -LiteralPath $base -Algorithm SHA256).Hash) {
    throw 'Base WeaponInfo.xml changed after staging. Run prepare-t007.ps1 again.'
}
if (Test-Path -LiteralPath $target) {
    throw "Existing overloader weapon data must be reviewed before installing: $target"
}
if (Test-Path -LiteralPath $receipt) {
    throw "An earlier T-007 receipt already exists: $receipt"
}
$check = New-Object System.Xml.XmlDocument
$check.Load($source)
foreach ($name in @('LF_GOLD_PISTOL', 'LF_GOLD_CARBINE', 'LF_GOLD_SHOTGUN')) {
    if ($check.SelectNodes("/weaponinfo/weapon[@type='$name']").Count -ne 1) {
        throw "Staged candidate definition is missing or duplicated: $name"
    }
}
$baseCheck = New-Object System.Xml.XmlDocument
$baseCheck.Load($base)
if ($check.SelectNodes('/weaponinfo/weapon').Count -ne
    $baseCheck.SelectNodes('/weaponinfo/weapon').Count + 3) {
    throw 'Staged weapon data should contain exactly three extra definitions.'
}
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $receipt) | Out-Null
Copy-Item -LiteralPath $source -Destination $target
$digest = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
Set-Content -LiteralPath $receipt -Value $digest -NoNewline
Write-Host "Installed T-007 data: $target"
Write-Host "Rollback with ./tools/remove-t007.ps1 -GameDirectory '$game' while the game is closed."
