param([string] $FrameworkDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'GTAIV-Reborn'))
$ErrorActionPreference = 'Stop'
$workspace = & (Join-Path $PSScriptRoot 'prepare-workspace.ps1') -FrameworkDirectory $FrameworkDirectory
& python (Join-Path $workspace 'tools/checks/checks.py') plan
if ($LASTEXITCODE -ne 0) { throw 'Check plan generation failed.' }
Copy-Item -LiteralPath (Join-Path $workspace 'docs/testing/LOCAL_VERIFICATION_PLAN.md') -Destination (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/testing/LOCAL_VERIFICATION_PLAN.md') -Force
Write-Host 'Updated the Liberty+ plan with links to the owning repositories.'
