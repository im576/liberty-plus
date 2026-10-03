param([string] $BaselineRevision)
# Lightweight source regression only; no main build, full verifier, package, installation or game process.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'tools/toolchains.ps1')
$compiler = Join-Path (Get-LibertyToolchain 'roslyn') 'csc.exe'
$sdk = Join-Path $repo 'sdk/Liberty.Sdk/bin/Liberty.Sdk.dll'
if (-not (Test-Path -LiteralPath $sdk)) { throw 'Existing SDK build required; this focused runner does not build the SDK.' }
$scratch = Join-Path $repo 'results-local/offline/lane-b-radial-snapshot'
if ($BaselineRevision) { $scratch = Join-Path $scratch 'baseline' }
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
Copy-Item -LiteralPath $sdk -Destination (Join-Path $scratch 'Liberty.Sdk.dll') -Force
$paths = @(
    'src/LibertyFramework/Engine/Services/UiService.cs',
    'src/LibertyFramework/Engine/Ui/RadialMenuView.cs',
    'src/LibertyFramework/Engine/Ui/MenuInput.cs',
    'src/LibertyFramework/Engine/ResourceLedger.cs',
    'src/LibertyFramework/Arsenal/Ui/StorageWheel.cs',
    'src/LibertyFramework/Arsenal/Logic/WeaponWheelLogic.cs',
    'src/LibertyFramework/Arsenal/Contracts/WeaponRecord.cs',
    'src/LibertyFramework/Arsenal/Contracts/WeaponCategory.cs',
    'src/LibertyFramework/Arsenal/Contracts/BodySlot.cs'
)
$sources = @()
foreach ($path in $paths) {
    if ($BaselineRevision) {
        $contents = & git -C $repo show "${BaselineRevision}:$path"
        if ($LASTEXITCODE -ne 0) { throw "Cannot read baseline $path" }
        $copy = Join-Path $scratch ([IO.Path]::GetFileName($path))
        [IO.File]::WriteAllLines($copy, [string[]]$contents)
        $sources += $copy
    } else { $sources += Join-Path $repo $path }
}
$sources += (Get-ChildItem -LiteralPath (Join-Path $repo 'tools/verify/radial-snapshot') -Filter '*.cs').FullName
$output = Join-Path $scratch 'OfflineVerify.exe'
Write-Output "Focused SDK SHA256: $((Get-FileHash -LiteralPath $sdk -Algorithm SHA256).Hash)"
Invoke-LibertyManaged $compiler /nologo /target:exe /platform:x86 /langversion:7.3 /warn:4 /warnaserror+ "/out:$output" "/reference:$sdk" /reference:System.Core.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Focused radial compile failed: $LASTEXITCODE" }
Invoke-LibertyManaged $output
if ($LASTEXITCODE -ne 0) { throw "Focused radial regression failed: $LASTEXITCODE" }
