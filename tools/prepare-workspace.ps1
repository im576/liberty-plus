param([string] $FrameworkDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'GTAIV-Reborn'))
$ErrorActionPreference = 'Stop'
$mod = Split-Path -Parent $PSScriptRoot
$framework = (Resolve-Path -LiteralPath $FrameworkDirectory).Path
$workspace = & python (Join-Path $framework 'tools\repository\workspace.py') --framework $framework --mod $mod
if ($LASTEXITCODE -ne 0) { throw 'Integration workspace assembly failed.' }
return [string]$workspace
