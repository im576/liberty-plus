param(
    [string] $FrameworkDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'GTAIV-Reborn'),
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $ScriptHookDotNetReference,
    [string] $LvsDirectory,
    [switch] $Fast
)
$ErrorActionPreference = 'Stop'
$workspace = & (Join-Path $PSScriptRoot 'prepare-workspace.ps1') -FrameworkDirectory $FrameworkDirectory
$packageArgs = @{ GameDirectory = $GameDirectory; ScriptHookDotNetReference = $ScriptHookDotNetReference; Fast = $Fast }
if ($LvsDirectory) { $packageArgs.LvsDirectory = $LvsDirectory }
& (Join-Path $workspace 'tools\package-phase2.ps1') @packageArgs
if ($LASTEXITCODE -ne 0) { throw 'Liberty+ packaging failed.' }
Write-Host "Package (not installed): $(Join-Path $workspace 'staging\phase2')"
