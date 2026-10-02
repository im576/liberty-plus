param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory,
    [Parameter(Mandatory = $true)]
    [string] $ScriptHookDotNetReference,
    # Extracted Liberty Vehicle Services CE release (MIT, ekzestean); bundled as the vehicle base for Arsenal trunks.
    [string] $LvsDirectory
)

# Builds the complete Phase 1 package into staging/phase1 without touching the game directory:
#   1. compile LibertyFramework.net.dll          4. derive WeaponInfo.xml + default.dat from the installed files
#   2. run offline verification (tools/verify)    5. copy config/presets/locations
#   3. build the gold pistol finish (IMG + IDE)   6. write staging/phase1/manifest.json with SHA-256 of every file
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$stage = Join-Path $repoRoot 'staging\phase1'

& (Join-Path $PSScriptRoot '..\build.ps1') -ScriptHookDotNetReference $ScriptHookDotNetReference
& (Join-Path $PSScriptRoot '..\verify.ps1') -GameDirectory $game | Select-String -Pattern '^(FAIL|RESULT)'

foreach ($child in @('scripts', 'update', 'previews', 'plugins')) {
    $path = Join-Path $stage $child
    if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path -Recurse -Force }
}
& (Join-Path $PSScriptRoot '..\build-finishes.ps1') -GameDirectory $game -StagingDirectory $stage

# WeaponInfo.xml: the installed T-007 override (hash-checked against its receipt) with the gold pistol
# pointed at the finish variant model. Carbine and shotgun are unchanged.
$installedWeaponInfo = Join-Path $game 'update\common\data\WeaponInfo.xml'
$receipt = Join-Path $game 'scripts\LibertyFramework\t007-weaponinfo.sha256'
if ((Get-FileHash -LiteralPath $installedWeaponInfo -Algorithm SHA256).Hash -ne (Get-Content -LiteralPath $receipt -Raw).Trim()) {
    throw 'Installed WeaponInfo.xml does not match the T-007 receipt; refusing to derive Phase 1 data from it.'
}
$catalog = Get-Content -LiteralPath (Join-Path $repoRoot 'assets\finishes\finishes.json') -Raw | ConvertFrom-Json
$document = New-Object System.Xml.XmlDocument
$document.PreserveWhitespace = $true
$document.Load($installedWeaponInfo)
foreach ($variant in $catalog.variants) {
    $assets = $document.SelectSingleNode("/weaponinfo/weapon[@type='$($variant.weaponInfoType)']/assets")
    if ($null -eq $assets) { throw "WeaponInfo entry $($variant.weaponInfoType) not found" }
    if ($assets.GetAttribute('model') -ne $variant.baseModel) { throw "$($variant.weaponInfoType) model is '$($assets.GetAttribute('model'))', expected $($variant.baseModel)" }
    $assets.SetAttribute('model', $variant.variantModel)
}
$stagedWeaponInfo = Join-Path $stage 'update\common\data\WeaponInfo.xml'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $stagedWeaponInfo) | Out-Null
$document.Save($stagedWeaponInfo)

# default.dat: the installed update copy (currently Various Pedestrian Actions') plus one IDE line,
# placed before WEAPONINFO so the variant model exists when weapon data is read.
$installedDefault = Join-Path $game 'update\common\data\default.dat'
$lines = [IO.File]::ReadAllLines($installedDefault)
$ideLine = 'IDE common:/data/lf_finishes.ide'
if ($lines -contains $ideLine) { throw 'Installed default.dat already references lf_finishes.ide' }
$anchor = [Array]::IndexOf($lines, 'IDE common:/data/peds.ide')
if ($anchor -lt 0) { throw 'IDE common:/data/peds.ide not found in installed update default.dat' }
$weaponInfoLine = [Array]::FindIndex($lines, [Predicate[string]]{ param($line) $line -like 'WEAPONINFO *' })
if ($weaponInfoLine -le $anchor) { throw 'Unexpected default.dat order (WEAPONINFO before peds.ide)' }
$newLines = New-Object System.Collections.Generic.List[string]
$newLines.AddRange([string[]]$lines[0..$anchor])
$newLines.Add($ideLine)
if ($anchor + 1 -lt $lines.Length) { $newLines.AddRange([string[]]$lines[($anchor + 1)..($lines.Length - 1)]) }
$stagedDefault = Join-Path $stage 'update\common\data\default.dat'
[IO.File]::WriteAllText($stagedDefault, ([string]::Join("`r`n", $newLines) + "`r`n"), (New-Object Text.ASCIIEncoding))

# Scripts and configuration.
$scriptsStage = Join-Path $stage 'scripts'
New-Item -ItemType Directory -Force -Path (Join-Path $scriptsStage 'LibertyFramework\config\presets') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $scriptsStage 'LibertyFramework\config\devtools') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll') -Destination (Join-Path $scriptsStage 'LibertyFramework.net.dll')
Copy-Item -LiteralPath (Join-Path $repoRoot 'config\gunplay.json') -Destination (Join-Path $scriptsStage 'LibertyFramework\config\gunplay.json')
Copy-Item -LiteralPath (Join-Path $repoRoot 'config\probe.json') -Destination (Join-Path $scriptsStage 'LibertyFramework\config\probe.json')
Copy-Item -LiteralPath (Join-Path $repoRoot 'config\devtools\locations.json') -Destination (Join-Path $scriptsStage 'LibertyFramework\config\devtools\locations.json')
Get-ChildItem -LiteralPath (Join-Path $repoRoot 'config\presets') -Filter '*.json' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $scriptsStage "LibertyFramework\config\presets\$($_.Name)")
}
# Arsenal (T-020) and holster (T-021) configs, when present.
foreach ($name in @('arsenal.json', 'holsters.json')) {
    $source = Join-Path $repoRoot "config\$name"
    if (Test-Path -LiteralPath $source) { Copy-Item -LiteralPath $source -Destination (Join-Path $scriptsStage "LibertyFramework\config\$name") }
}

# Liberty Vehicle Services CE: the author's scripts folder plus the dashboard bridge it DllImports.
# The LibertyCityPlates bridge is omitted (LibertyCityPlates is not installed).
if ($LvsDirectory) {
    $lvs = (Resolve-Path -LiteralPath $LvsDirectory).Path
    if (-not (Test-Path -LiteralPath (Join-Path $lvs 'scripts\LibertyVehicleServicesCE.CS'))) { throw "Not an LVS release folder: $lvs" }
    Copy-Item -LiteralPath (Join-Path $lvs 'scripts\LibertyVehicleServicesCE.CS') -Destination $scriptsStage
    Copy-Item -LiteralPath (Join-Path $lvs 'scripts\LibertyVehicleServicesCE.ini') -Destination $scriptsStage
    Copy-Item -LiteralPath (Join-Path $lvs 'scripts\LibertyVehicleServicesCE') -Destination $scriptsStage -Recurse
    New-Item -ItemType Directory -Force -Path (Join-Path $stage 'plugins') | Out-Null
    Copy-Item -LiteralPath (Join-Path $lvs 'plugins\000_LVSCE_Dashboard_Bridge.asi') -Destination (Join-Path $stage 'plugins')
    # MIT requires the licence notice to travel with the files.
    New-Item -ItemType Directory -Force -Path (Join-Path $scriptsStage 'LibertyVehicleServicesCE') | Out-Null
    Copy-Item -LiteralPath (Join-Path $lvs 'LICENSE') -Destination (Join-Path $scriptsStage 'LibertyVehicleServicesCE\LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $lvs 'CREDITS.md') -Destination (Join-Path $scriptsStage 'LibertyVehicleServicesCE\CREDITS.md')
}

# Manifest: relative path, SHA-256, and how the installer treats an existing file.
$entries = @()
Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.FullName -notmatch '\\previews\\' -and $_.Name -ne 'manifest.json' } | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($stage.Length + 1)
    $policy = 'replace'
    if ($relative -match '^scripts\\LibertyFramework\\config\\(probe\.json|devtools\\locations\.json)$') { $policy = 'keep-existing' }
    # The player's own LVS settings survive reinstalls.
    if ($relative -eq 'scripts\LibertyVehicleServicesCE.ini') { $policy = 'keep-existing' }
    $entries += [ordered]@{ path = $relative; sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash; policy = $policy }
}
$manifest = [ordered]@{
    package = 'liberty-framework-phase1'
    builtUtc = (Get-Date).ToUniversalTime().ToString('o')
    gameVersion = '1.2.0.59'
    baseWeaponInfoSha256 = (Get-FileHash -LiteralPath $installedWeaponInfo -Algorithm SHA256).Hash
    baseDefaultDatSha256 = (Get-FileHash -LiteralPath $installedDefault -Algorithm SHA256).Hash
    files = $entries
}
[IO.File]::WriteAllText((Join-Path $stage 'manifest.json'), ($manifest | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
Write-Host "Staged $($entries.Count) files in $stage"
$entries | ForEach-Object { Write-Host ("  {0,-14} {1}" -f $_.policy, $_.path) }
