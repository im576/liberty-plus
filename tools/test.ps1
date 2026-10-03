param(
    [string] $FrameworkDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'GTAIV-Reborn'),
    [switch] $NoGame,
    [string] $GameDirectory,
    [string] $Filter = '*'
)
$ErrorActionPreference = 'Stop'
if (-not $NoGame -and -not $GameDirectory) { throw 'Pass -NoGame or -GameDirectory. This never starts the game.' }
$workspace = & (Join-Path $PSScriptRoot 'prepare-workspace.ps1') -FrameworkDirectory $FrameworkDirectory
Write-Host "Evidence workspace: $workspace"
$verifyArgs = if ($NoGame) { @{ NoGame = $true } } else { @{ GameDirectory = $GameDirectory } }
& (Join-Path $workspace 'tools\verify.ps1') @verifyArgs
if ($LASTEXITCODE -ne 0) { throw 'Combined offline verification failed.' }
& (Join-Path $workspace 'tools\tests\Run-Tests.ps1') -Filter $Filter -ResultsPath (Join-Path $workspace 'suite-results.json')
if ($LASTEXITCODE -ne 0) { throw 'Tooling tests failed.' }
& python (Join-Path $workspace 'tools\checks\checks.py') validate
if ($LASTEXITCODE -ne 0) { throw 'Check queue validation failed.' }
& python (Join-Path $FrameworkDirectory 'tools\art\artq.py') --root (Split-Path -Parent $PSScriptRoot) validate
if ($LASTEXITCODE -ne 0) { throw 'Liberty+ artwork validation failed.' }
Write-Host 'Liberty+ offline checks PASS. Game behavior remains untested.'
