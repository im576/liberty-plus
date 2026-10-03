# Regenerates config/presets/*.json from the weapon profiles in config/gunplay.json.
# Each preset scales a few recoil/spread fields; edit the factors below, then rerun tools/verify.ps1.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$config = Get-Content -LiteralPath (Join-Path $repoRoot 'config\gunplay.json') -Raw | ConvertFrom-Json
$presetDirectory = Join-Path $repoRoot 'config\presets'
New-Item -ItemType Directory -Force -Path $presetDirectory | Out-Null

function Write-Preset($id, $name, $description, $kick, $recoveryRate, $recoveryFraction, $growth, $bloom, $bloomRecovery, $suffix) {
    $weapons = @()
    foreach ($weapon in $config.weapons) {
        $recoil = $weapon.recoil.PSObject.Copy()
        $spread = $weapon.spread.PSObject.Copy()
        $recoil.verticalKickDegrees = [math]::Round([double]$recoil.verticalKickDegrees * $kick, 3)
        $recoil.horizontalKickDegrees = [math]::Round([double]$recoil.horizontalKickDegrees * $kick, 3)
        $recoil.horizontalRandomDegrees = [math]::Round([double]$recoil.horizontalRandomDegrees * $kick, 3)
        $recoil.recoveryDegreesPerSecond = [math]::Round([double]$recoil.recoveryDegreesPerSecond * $recoveryRate, 3)
        $recoil.recoveryFraction = [math]::Round([math]::Min([double]1.0, [double]$recoil.recoveryFraction * $recoveryFraction), 3)
        $recoil.sustainedFireGrowthPerShot = [math]::Round([double]$recoil.sustainedFireGrowthPerShot * $growth, 3)
        $spread.perShotDegrees = [math]::Round([double]$spread.perShotDegrees * $bloom, 3)
        $spread.recoveryDegreesPerSecond = [math]::Round([double]$spread.recoveryDegreesPerSecond * $bloomRecovery, 3)
        $spread.shortBurstRecoveryDegreesPerSecond = [math]::Round([double]$spread.shortBurstRecoveryDegreesPerSecond * $bloomRecovery, 3)
        $weapons += [ordered]@{
            weaponId = $weapon.weaponId
            profileName = ($weapon.profileName -replace '_gta4plus', "_$suffix")
            recoil = $recoil
            spread = $spread
        }
    }
    $preset = [ordered]@{ schemaVersion = 1; name = $name; description = $description; weapons = $weapons }
    [IO.File]::WriteAllText((Join-Path $presetDirectory "$id.json"), ($preset | ConvertTo-Json -Depth 6), (New-Object Text.UTF8Encoding($false)))
    Write-Host "Wrote preset $id"
}

Write-Preset 'a_gta4plus' 'A - GTA IV+' 'Shipped defaults: GTA IV physicality with readable modern kick.' 1 1 1 1 1 1 'gta4plus'
Write-Preset 'b_mafia_light' 'B - Mafia Inspired Light' 'Lighter kick, faster recovery, tighter sustained fire.' 0.75 1.3 1.05 0.8 0.8 1.25 'mafia_light'
Write-Preset 'c_mafia_heavy' 'C - Mafia Inspired Heavy' 'Heavier kick, less auto-recovery, faster bloom: bursts need active correction.' 1.35 0.85 0.8 1.3 1.25 0.85 'mafia_heavy'
Write-Preset 'd_experimental' 'D - Experimental' 'Strong vertical kick with near-full recovery; test snappy feel.' 1.6 1.8 1.1 0.6 1.0 1.0 'experimental'
