param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $ArchivePath,
    [string] $SevenZip = 'D:\GTAIV-Reborn-Tools\toolchains\7zip\Files\7-Zip\7z.exe'
)

# W-1: installs the owner-downloaded "Realistic Weapon Overhaul Mod" (Nexus GTA IV mod 272, konarider26; models and
# textures by lenol03 and Push, animations by BDawg, sounds by lenol03 and SparktimusPrime - see third_party/README.md)
# as the replacement arsenal for every vanilla firearm:
# - pc\models\cdimages\weapons.img and pc\anim\anim.img are replaced (originals backed up);
# - the weapon sound bank goes through FusionFix's RPF overload folder update\pc\audio\sfx\resident.rpf\RESIDENT\WEAPONS
#   (the original resident.rpf is never modified);
# - update\common\data\WeaponInfo.xml = the pack's vanilla weapon entries + Liberty Framework's LF_GOLD_* entries.
# Rollback: tools/rollback-weapon-pack.ps1 with the printed backup folder.
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }
$known = '3E5268AED100E14BEF9BC886EC47C0C154CAB3E1191C06A600FECEB9B7F4949F'
if ((Get-FileHash -LiteralPath $ArchivePath).Hash -ne $known) { throw 'Archive hash is not the inspected Realistic Weapon Overhaul 1.0.' }

$work = Join-Path ([IO.Path]::GetTempPath()) ('lf-weapons-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    & $SevenZip x $ArchivePath "-o$work" -y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw '7-Zip extraction failed' }
    $pack = Join-Path $work 'Realistic Weapon Overhaul Mod'

    # Merged WeaponInfo: pack entries for vanilla weapons, plus every LF_* weapon from the installed file.
    $installedInfo = Join-Path $game 'update\common\data\WeaponInfo.xml'
    $merged = [xml](Get-Content -LiteralPath (Join-Path $pack 'WeaponInfo.xml') -Raw)
    $ours = [xml](Get-Content -LiteralPath $installedInfo -Raw)
    $added = 0
    foreach ($weapon in $ours.SelectNodes('//weapon')) {
        if ($weapon.type -notlike 'LF_*') { continue }
        $parent = $merged.SelectSingleNode('//weapon').ParentNode
        [void]$parent.AppendChild($merged.ImportNode($weapon, $true)); $added++
    }
    if ($added -lt 1) { throw 'No LF_* weapons found in the installed WeaponInfo.xml' }
    $mergedPath = Join-Path $work 'WeaponInfo.merged.xml'
    $settings = New-Object Xml.XmlWriterSettings; $settings.Indent = $true; $settings.Encoding = New-Object Text.UTF8Encoding($false)
    $writer = [Xml.XmlWriter]::Create($mergedPath, $settings); $merged.Save($writer); $writer.Close()

    $files = @(
        @{ Source = (Join-Path $pack 'weapons.img'); Target = 'pc\models\cdimages\weapons.img' },
        @{ Source = (Join-Path $pack 'anim.img'); Target = 'pc\anim\anim.img' },
        @{ Source = (Join-Path $pack 'WEAPONS'); Target = 'update\pc\audio\sfx\resident.rpf\RESIDENT\WEAPONS' },
        @{ Source = $mergedPath; Target = 'update\common\data\WeaponInfo.xml' }
    )
    $backup = Join-Path $game ('scripts\LibertyFramework\backups\weapon-pack-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $backup | Out-Null
    $manifest = @()
    foreach ($file in $files) {
        $target = Join-Path $game $file.Target
        $existed = Test-Path -LiteralPath $target
        if ($existed) {
            $saved = Join-Path $backup $file.Target
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $saved) | Out-Null
            Copy-Item -LiteralPath $target -Destination $saved
        }
        $manifest += [pscustomobject]@{ Path = $file.Target; Existed = $existed; Installed = (Get-FileHash -LiteralPath $file.Source).Hash }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $backup 'manifest.json') -Encoding UTF8
    foreach ($file in $files) {
        $target = Join-Path $game $file.Target
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $file.Source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.Source).Hash) { throw "Hash mismatch: $($file.Target)" }
        Write-Host "  installed $($file.Target)"
    }
    Write-Host "Weapon pack installed ($added LF weapons kept). Backup: $backup"
}
finally { Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue }
