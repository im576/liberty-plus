param(
    # Folder containing GTAIV.exe. Omit it with -NoGame (the cloud container): the sections that read the game's exe and
    # archives are then reported NOT-RUN and the result is not a full verification.
    [string] $GameDirectory,
    [switch] $NoGame
)

# Offline verification: builds tools/verify/OfflineVerify.exe from the game-independent sources and
# runs it against GTAIV.exe on disk. Does not start or modify the game.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'toolchains.ps1')
if ($NoGame) { $exe = '--no-game' }
elseif ($GameDirectory) { $exe = Join-Path (Resolve-Path -LiteralPath $GameDirectory).Path 'GTAIV.exe' }
else { throw 'Pass -GameDirectory <GTAIV folder>, or -NoGame for the repository-only checks' }
# The pinned Roslyn (C# 7.3) on every platform, the same compiler and language level as tools/build.ps1: one toolchain
# for the repository, so code the build accepts is code the verifier accepts.
$compiler = Join-Path (Get-LibertyToolchain 'roslyn') 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw "C# compiler not found: $compiler (run tools/get-toolchains.ps1)" }
$binDirectory = Join-Path $repoRoot 'tools\verify\bin'
$output = Join-Path $binDirectory 'OfflineVerify.exe'
$src = Join-Path $repoRoot 'src\LibertyFramework'
New-Item -ItemType Directory -Force -Path $binDirectory | Out-Null

# The verifier's own copy of the SDK (engine logic under test is compiled against it; the SDK lets OfflineVerify see its
# internals, as it does the engine). Built here so the verifier never depends on an earlier tools/build.ps1 run.
$sdkRoot = Join-Path $repoRoot 'sdk\Liberty.Sdk'
$sdkDll = Join-Path $binDirectory 'Liberty.Sdk.dll'
$sdkSources = @(Get-ChildItem -LiteralPath $sdkRoot -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Sort-Object FullName | ForEach-Object { $_.FullName })
# Only sources without ScriptHookDotNet dependencies may be listed here.
$sources = @(
    (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools\verify') -Filter '*.cs').FullName
    (Join-Path $src 'Core\Memory\IMemory.cs')
    (Join-Path $src 'Core\Memory\MemoryReader.cs')
    (Join-Path $src 'Core\Memory\CodeScanner.cs')
    (Join-Path $src 'Core\Memory\GameAddresses.cs')
    (Join-Path $src 'Core\Config\JsonStore.cs')
    (Join-Path $src 'Engine\ModuleReloader.cs')
    (Join-Path $src 'Engine\EngineConfig.cs')
    (Join-Path $src 'Core\Math3\Vec3.cs')
    (Join-Path $src 'GameApi\WeaponInfoXml.cs')
    (Join-Path $src 'GameApi\WeaponStats.cs')
    (Join-Path $src 'Engine\Locations\LocationFile.cs')
    (Join-Path $src 'Engine\Locations\TeleportLocation.cs')
    (Get-ChildItem -LiteralPath (Join-Path $src 'Gunplay\Profiles') -Filter '*.cs').FullName
    (Get-ChildItem -LiteralPath (Join-Path $src 'Gunplay\Recoil') -Filter '*.cs').FullName
    (Get-ChildItem -LiteralPath (Join-Path $src 'Gunplay\Spread') -Filter '*.cs').FullName
    (Get-ChildItem -LiteralPath (Join-Path $src 'Arsenal\Contracts') -Filter '*.cs').FullName
    # T-023 vehicle extras catalog (reads the installed vehicles.img through the finishes IMG/RSC readers).
    (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools\finishes') -Filter '*.cs' | Where-Object { $_.Name -ne 'Program.cs' }).FullName
    (Join-Path $repoRoot 'tools\vehicles\VehicleExtrasScanner.cs')
    (Join-Path $src 'CombatEffects\CombatEffectsConfig.cs')
    (Join-Path $src 'GameApi\SkeletonCollapseEngine.cs')
    (Join-Path $src 'GameApi\DirectNatives.cs')
    # Engine plumbing without ScriptHookDotNet: event bus, scheduler, resource ledger, command registry (engine audit).
    (Join-Path $src 'Core\Config\LibertyPaths.cs')
    (Join-Path $src 'Engine\Events\EventBus.cs')
    (Join-Path $src 'Engine\Scheduling\Scheduler.cs')
    (Join-Path $src 'Engine\ResourceLedger.cs')
    (Join-Path $src 'Engine\Services\CommandRegistry.cs')
    (Join-Path $src 'Engine\Services\ConfigService.cs')
    (Join-Path $src 'Engine\Services\StateService.cs')
    # The core's C ABI mirror and snapshot accessors, checked against native/LibertyCore/include/liberty_core.h.
    (Join-Path $src 'Engine\Core\CoreAbi.cs')
    (Join-Path $src 'Engine\Core\CoreBridge.cs')
    # Any folder named Logic holds ScriptHookDotNet-free code that the verifier can test (T-020/T-021 onward).
    (Get-ChildItem -LiteralPath $src -Recurse -Directory -Filter 'Logic' | ForEach-Object { (Get-ChildItem -LiteralPath $_.FullName -Filter '*.cs').FullName })
    # SDK mods' Logic folders too (T-033: mods/Liberty.World/Logic); they reference only Liberty.Sdk.
    (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'mods') -Recurse -Directory -Filter 'Logic' | ForEach-Object { (Get-ChildItem -LiteralPath $_.FullName -Filter '*.cs').FullName })
)
# Cache compilation only: the verifier itself still executes on every invocation against current config/game files.
Import-Module (Join-Path $PSScriptRoot 'BuildCache.psm1') -Force
$stamp = Join-Path $binDirectory '.verify-build-inputs'
$key = Get-InputKey $repoRoot (@($sdkSources) + @($sources) + @($PSCommandPath, (Join-Path $PSScriptRoot 'toolchains.ps1')) +
    @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs/sdk/examples') -Filter '*.cs' | ForEach-Object { $_.FullName })) @("csc|$(Get-FileIdentity $compiler)")
$outputs = @($sdkDll, $output, (Join-Path $binDirectory 'SdkExamples.dll'))
if (-not (Test-BuildStamp $stamp $key $outputs)) {
    Clear-BuildStamp $stamp
    Invoke-LibertyManaged $compiler /nologo /target:library /platform:anycpu /langversion:7.3 /warn:4 /warnaserror+ /nowarn:1591 "/out:$sdkDll" /reference:System.Core.dll $sdkSources
    if ($LASTEXITCODE -ne 0) { throw "Liberty.Sdk (verifier copy) failed to compile (exit $LASTEXITCODE)" }
    # The SDK examples the docs show (docs/sdk/examples) must compile against the current SDK, warnings as errors.
    $examples = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'docs\sdk\examples') -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.FullName })
    Invoke-LibertyManaged $compiler /nologo /target:library /platform:anycpu /langversion:7.3 /warn:4 /warnaserror+ "/out:$(Join-Path $binDirectory 'SdkExamples.dll')" "/reference:$sdkDll" /reference:System.Core.dll $examples
    if ($LASTEXITCODE -ne 0) { throw "SDK examples (docs/sdk/examples) failed to compile against the current SDK (exit $LASTEXITCODE)" }

    Invoke-LibertyManaged $compiler /nologo /target:exe /platform:x86 /unsafe /langversion:7.3 /warn:4 "/out:$output" "/reference:$sdkDll" /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll /reference:System.Drawing.dll /reference:System.Core.dll $sources
    if ($LASTEXITCODE -ne 0) { throw "Verifier build failed with exit code $LASTEXITCODE" }
    Set-BuildStamp $stamp $key
} else { Write-Host 'Offline verifier compiler outputs up to date; executing current checks.' }
Invoke-LibertyManaged $output $exe $repoRoot
if ($LASTEXITCODE -ne 0) { throw "Offline verification failed (exit $LASTEXITCODE)" }
