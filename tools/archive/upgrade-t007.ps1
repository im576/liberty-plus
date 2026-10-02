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
$backupDirectory = Join-Path $game 'scripts\LibertyFramework\backups'

if (Get-Process GTAIV -ErrorAction SilentlyContinue) {
    throw 'GTA IV is running. Close it before upgrading T-007 data.'
}
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.2.0.59') {
    throw 'T-007 is prepared only for GTAIV.exe 1.2.0.59.'
}
if (-not (Test-Path -LiteralPath $fusionConfig) -or
    (Get-Content -LiteralPath $fusionConfig -Raw) -notmatch '(?m)^\s*ExtendedLimits\s*=\s*1(?:\s|$)') {
    throw 'FusionFix ExtendedLimits=1 is required for custom weapon registration.'
}
if (-not (Test-Path -LiteralPath $target) -or -not (Test-Path -LiteralPath $receipt)) {
    throw 'The first T-007 override and receipt must both exist before upgrading.'
}
$oldHash = (Get-Content -LiteralPath $receipt -Raw).Trim()
if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $oldHash) {
    throw 'Existing T-007 data changed after installation; no files were modified.'
}
if ((Get-Content -LiteralPath $baseHashFile -Raw).Trim() -ne
    (Get-FileHash -LiteralPath $base -Algorithm SHA256).Hash) {
    throw 'Base WeaponInfo.xml changed after staging. Run prepare-t007.ps1 again.'
}
$check = New-Object System.Xml.XmlDocument
$check.Load($source)
$baseCheck = New-Object System.Xml.XmlDocument
$baseCheck.Load($base)
foreach ($name in @('LF_GOLD_PISTOL', 'LF_GOLD_CARBINE', 'LF_GOLD_SHOTGUN')) {
    if ($check.SelectNodes("/weaponinfo/weapon[@type='$name']").Count -ne 1) {
        throw "Staged candidate definition is missing or duplicated: $name"
    }
}
if ($check.SelectNodes('/weaponinfo/weapon').Count -ne
    $baseCheck.SelectNodes('/weaponinfo/weapon').Count + 3) {
    throw 'Staged weapon data should contain exactly three extra definitions.'
}

New-Item -ItemType Directory -Force -Path $backupDirectory | Out-Null
$backupName = 'WeaponInfo.t007-pistol.' + (Get-Date -Format 'yyyyMMdd-HHmmss') +
    '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.xml'
$backup = Join-Path $backupDirectory $backupName
Copy-Item -LiteralPath $target -Destination $backup
try {
    Copy-Item -LiteralPath $source -Destination $target -Force
    $newHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    Set-Content -LiteralPath $receipt -Value $newHash -NoNewline
}
catch {
    Copy-Item -LiteralPath $backup -Destination $target -Force
    Set-Content -LiteralPath $receipt -Value $oldHash -NoNewline
    throw
}
Write-Host "Upgraded T-007 weapon data: $target"
Write-Host "Previous pistol-only override backed up: $backup"
