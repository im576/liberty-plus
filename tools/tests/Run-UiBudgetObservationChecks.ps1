# Focused compile/test only, using existing SDK; no full verifier/build/package/game.
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $repo 'tools/toolchains.ps1')
$sdk = Join-Path $repo 'sdk/Liberty.Sdk/bin/Liberty.Sdk.dll'
if (-not (Test-Path -LiteralPath $sdk)) { throw 'Existing SDK DLL required; focused runner does not build SDK.' }
$scratch = Join-Path $repo 'results-local/offline/lane-b-ui-budget-observation'
New-Item -ItemType Directory -Force -Path $scratch | Out-Null
Copy-Item -LiteralPath $sdk -Destination (Join-Path $scratch 'Liberty.Sdk.dll') -Force
$sources = @(
    'src/LibertyFramework/Engine/Services/PerfService.cs',
    'src/LibertyFramework/Core/Performance/Logic/CostMeter.cs',
    'src/LibertyFramework/Engine/Ui/Logic/UiBudgetLogic.cs',
    'src/LibertyFramework/Core/Logging/RuntimeLog.cs',
    'src/LibertyFramework/Engine/ResourceLedger.cs',
    'src/LibertyFramework/Arsenal/Ui/TrunkSequence.cs',
    'src/LibertyFramework/Arsenal/Logic/TrunkTimings.cs'
) | ForEach-Object { Join-Path $repo $_ }
$sources += (Get-ChildItem -LiteralPath (Join-Path $repo 'tools/verify/ui-budget-observation') -Filter '*.cs').FullName
$output = Join-Path $scratch 'OfflineVerify.exe'
Write-Output "Focused SDK SHA256: $((Get-FileHash -LiteralPath $sdk -Algorithm SHA256).Hash)"
Invoke-LibertyManaged (Join-Path (Get-LibertyToolchain 'roslyn') 'csc.exe') /nologo /target:exe /platform:x86 /langversion:7.3 /warn:4 /warnaserror+ "/out:$output" "/reference:$sdk" /reference:System.Core.dll /reference:System.Runtime.Serialization.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Observation compile failed: $LASTEXITCODE" }
Invoke-LibertyManaged $output $scratch
if ($LASTEXITCODE -ne 0) { throw "Observation checks failed: $LASTEXITCODE" }
