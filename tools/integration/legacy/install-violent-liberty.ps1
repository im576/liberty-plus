param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $ArchivePath,
    [string] $BuiltDllPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$archive = (Resolve-Path -LiteralPath $ArchivePath).Path
$dll = (Resolve-Path -LiteralPath $BuiltDllPath).Path
$expectedArchiveHash = '459A66BBDE3BBF48C80092CA57C34455BA5FDA770B628835CB6D160C3AB7A2A6'
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'Close GTA IV before installing.' }
if ((Get-Item -LiteralPath (Join-Path $game 'GTAIV.exe')).VersionInfo.FileVersion -ne '1.2.0.59') { throw 'This install requires GTA IV 1.2.0.59.' }
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedArchiveHash) { throw 'Violent Liberty archive hash differs from the inspected release.' }

$backupRoot = Join-Path $game ('scripts\LibertyFramework\backups\violent-liberty-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path (Join-Path $backupRoot 'incoming') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $backupRoot 'original') -Force | Out-Null
$sourceNames = @{
    'plugins\ViolentLiberty.asi' = 'GameFiles/plugins/ViolentLiberty.asi'
    'plugins\ViolentLiberty.ini' = 'GameFiles/plugins/ViolentLiberty.ini'
    'pc\textures\fxprojtex.wtd' = 'GameFiles/pc/textures/fxprojtex.wtd'
}
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($pair in $sourceNames.GetEnumerator()) {
        $entry = $zip.GetEntry($pair.Value)
        if ($null -eq $entry) { throw "Archive is missing $($pair.Value)" }
        $destination = Join-Path (Join-Path $backupRoot 'incoming') $pair.Key
        New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
        $inputStream = $entry.Open()
        $outputStream = [IO.File]::Create($destination)
        try { $inputStream.CopyTo($outputStream) }
        finally { $outputStream.Dispose(); $inputStream.Dispose() }
    }
}
finally { $zip.Dispose() }

# This is our local tuning overlay; the third-party INI itself stays outside the repository.
$tuning = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'config\violent_liberty_tuning.json') -Raw | ConvertFrom-Json
if ($tuning.headPressure -notin @('low','medium','high') -or $tuning.neckPressure -notin @('low','medium','high') -or
    $tuning.bleedDuration -notin @('short','long','random') -or
    $tuning.shotgunChanceMinimumPercent -lt 0 -or $tuning.shotgunChanceMaximumPercent -gt 100 -or
    $tuning.shotgunChanceMinimumPercent -gt $tuning.shotgunChanceMaximumPercent -or
    $tuning.shotgunGravitySpeedPercent -lt 50 -or $tuning.shotgunGravitySpeedPercent -gt 400) {
    throw 'Invalid Violent Liberty tuning values.'
}
$iniPath = Join-Path (Join-Path $backupRoot 'incoming') 'plugins\ViolentLiberty.ini'
$ini = [IO.File]::ReadAllText($iniPath)
$iniValues = @{
    HeadLowPressure = [int]($tuning.headPressure -eq 'low')
    HeadMediumPressure = [int]($tuning.headPressure -eq 'medium')
    HeadHighPressure = [int]($tuning.headPressure -eq 'high')
    NeckLowPressure = [int]($tuning.neckPressure -eq 'low')
    NeckMediumPressure = [int]($tuning.neckPressure -eq 'medium')
    NeckHighPressure = [int]($tuning.neckPressure -eq 'high')
    BleedDurationShort = [int]($tuning.bleedDuration -eq 'short')
    BleedDurationLong = [int]($tuning.bleedDuration -eq 'long')
    BleedDurationRandom = [int]($tuning.bleedDuration -eq 'random')
    ChanceMinimumPercent = [int]$tuning.shotgunChanceMinimumPercent
    ChanceMaximumPercent = [int]$tuning.shotgunChanceMaximumPercent
    GravitySpeedPercent = [int]$tuning.shotgunGravitySpeedPercent
}
foreach ($entry in $iniValues.GetEnumerator()) {
    $pattern = '(?m)^' + [regex]::Escape($entry.Key) + '=[0-9]+(?=\r?$)'
    if ([regex]::Matches($ini, $pattern).Count -ne 1) { throw "Cannot tune Violent Liberty setting $($entry.Key)" }
    $ini = [regex]::Replace($ini, $pattern, ($entry.Key + '=' + $entry.Value))
}
[IO.File]::WriteAllText($iniPath, $ini, (New-Object Text.UTF8Encoding($false)))

$configPath = 'scripts\LibertyFramework\config\combat_effects.json'
$config = Get-Content -LiteralPath (Join-Path $game $configPath) -Raw | ConvertFrom-Json
if ($config.schemaVersion -ne 1) { throw 'Unexpected combat effects config schema.' }
$defaults = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'config\combat_effects.json') -Raw | ConvertFrom-Json
foreach ($property in $defaults.PSObject.Properties) {
    if ($null -eq $config.PSObject.Properties[$property.Name])
        { $config | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value }
}
if ($null -eq $config.PSObject.Properties['bloodVisualMode']) { $config | Add-Member -NotePropertyName bloodVisualMode -NotePropertyValue 'external' }
else { $config.bloodVisualMode = 'external' }
$incomingConfig = Join-Path (Join-Path $backupRoot 'incoming') $configPath
New-Item -ItemType Directory -Path (Split-Path -Parent $incomingConfig) -Force | Out-Null
[IO.File]::WriteAllText($incomingConfig, ($config | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding($false)))

foreach ($item in @(
    @{ Path = 'plugins\GTAIV.EFLC.FusionFix.cfg'; Key = 'GraphicsAPI' },
    @{ Path = 'd3d9.cfg'; Key = 'API' }
)) {
    $current = Join-Path $game $item.Path
    $contents = [IO.File]::ReadAllText($current)
    $pattern = '(?m)^(' + [regex]::Escape($item.Key) + '\s*=\s*)[01](\s*)$'
    if ([regex]::Matches($contents, $pattern).Count -ne 1) { throw "Cannot identify renderer setting in $($item.Path)" }
    $updated = [regex]::Replace($contents, $pattern, '${1}1${2}')
    $incoming = Join-Path (Join-Path $backupRoot 'incoming') $item.Path
    New-Item -ItemType Directory -Path (Split-Path -Parent $incoming) -Force | Out-Null
    [IO.File]::WriteAllText($incoming, $updated, (New-Object Text.UTF8Encoding($false)))
}

$dllPath = 'scripts\LibertyFramework.net.dll'
$incomingDll = Join-Path (Join-Path $backupRoot 'incoming') $dllPath
New-Item -ItemType Directory -Path (Split-Path -Parent $incomingDll) -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination $incomingDll

$paths = @($sourceNames.Keys) + @($configPath, 'plugins\GTAIV.EFLC.FusionFix.cfg', 'd3d9.cfg', $dllPath)
$actions = @()
foreach ($relative in $paths) {
    $target = Join-Path $game $relative
    $exists = Test-Path -LiteralPath $target -PathType Leaf
    $oldHash = if ($exists) { (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash } else { $null }
    if ($exists) {
        $saved = Join-Path (Join-Path $backupRoot 'original') $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $saved) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $saved
        if ((Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash -ne $oldHash) { throw "Backup hash mismatch: $relative" }
    }
    $newHash = (Get-FileHash -LiteralPath (Join-Path (Join-Path $backupRoot 'incoming') $relative) -Algorithm SHA256).Hash
    $actions += [ordered]@{ path = $relative; hadOriginal = $exists; originalSha256 = $oldHash; installedSha256 = $newHash }
}
$receipt = [ordered]@{ package = 'violent-liberty-companion'; archiveSha256 = $expectedArchiveHash; createdUtc = (Get-Date).ToUniversalTime().ToString('o'); actions = $actions }
[IO.File]::WriteAllText((Join-Path $backupRoot 'rollback.json'), ($receipt | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
try {
    foreach ($action in $actions) {
        $source = Join-Path (Join-Path $backupRoot 'incoming') $action.path
        $target = Join-Path $game $action.path
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $action.installedSha256) { throw "Install hash mismatch: $($action.path)" }
        Write-Host "  installed $($action.path)"
    }
}
catch {
    $failure = $_
    & (Join-Path $PSScriptRoot 'rollback-violent-liberty.ps1') -GameDirectory $game -BackupDirectory $backupRoot
    throw $failure
}
Write-Host "Violent Liberty companion installed. Backup: $backupRoot"
