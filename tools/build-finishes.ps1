param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory,
    [string] $StagingDirectory
)

# Builds weapon finish variants (assets/finishes/finishes.json) from the local game files into
# staging/phase1. Read-only on the game directory; safe while GTA IV is running.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (-not $StagingDirectory) { $StagingDirectory = Join-Path $repoRoot 'staging\phase1' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$output = Join-Path $repoRoot 'tools\finishes\bin\FinishBuilder.exe'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $output) | Out-Null
$sources = (Get-ChildItem -LiteralPath (Join-Path $repoRoot 'tools\finishes') -Filter '*.cs').FullName
& $compiler /nologo /target:exe /platform:x86 /warn:4 /warnaserror+ "/out:$output" /reference:System.Runtime.Serialization.dll /reference:System.Drawing.dll /reference:System.Core.dll $sources
if ($LASTEXITCODE -ne 0) { throw "FinishBuilder build failed ($LASTEXITCODE)" }
& $output $game $repoRoot $StagingDirectory
if ($LASTEXITCODE -ne 0) { throw "FinishBuilder failed ($LASTEXITCODE)" }
