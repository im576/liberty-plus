param([Parameter(Mandatory = $true)][string] $ScriptHookDotNetReference)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $root 'workspace.json'))) {
    throw 'Run this entry point only in a generated integration workspace.'
}
& (Join-Path $PSScriptRoot 'build-framework.ps1') -ScriptHookDotNetReference $ScriptHookDotNetReference
if ($LASTEXITCODE -ne 0) { throw 'Framework compilation failed.' }
& (Join-Path $PSScriptRoot 'build-libertyplus.ps1') -FrameworkDirectory $root -ScriptHookDotNetReference $ScriptHookDotNetReference -SkipFrameworkBuild -SourceDirectory (Join-Path $root 'src\LibertyPlus') -OutputDirectory (Join-Path $root 'mods\LibertyPlus\bin')
if ($LASTEXITCODE -ne 0) { throw 'Liberty+ compilation failed.' }
