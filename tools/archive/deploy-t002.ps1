param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$exe = Join-Path $game 'GTAIV.exe'
$sourceDll = Join-Path $repoRoot 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll'
$sourceConfig = Join-Path $repoRoot 'config\probe.json'
$targetDll = Join-Path $game 'scripts\LibertyFramework.net.dll'
$targetConfig = Join-Path $game 'scripts\LibertyFramework\config\probe.json'

if (-not (Test-Path -LiteralPath $exe)) { throw "GTAIV.exe not found in $game" }
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.2.0.59') {
    throw 'This T-002 package is only prepared for GTAIV.exe 1.2.0.59.'
}
if (Get-Process GTAIV -ErrorAction SilentlyContinue) {
    throw 'GTA IV is running. Close it before deploying T-002.'
}
if (-not (Test-Path -LiteralPath (Join-Path $game 'ScriptHookDotNet.asi'))) {
    throw 'ScriptHookDotNet.asi is missing. Install and verify T-001 first.'
}
if (-not (Test-Path -LiteralPath $sourceDll)) { throw "Build the project first: $sourceDll" }
if (-not (Test-Path -LiteralPath $sourceConfig)) { throw "Sample config is missing: $sourceConfig" }

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetDll) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetConfig) | Out-Null
if (Test-Path -LiteralPath $targetDll) {
    if ((Get-FileHash -LiteralPath $targetDll -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $sourceDll -Algorithm SHA256).Hash) {
        $backup = "$targetDll.t001.bak"
        if (Test-Path -LiteralPath $backup) {
            $suffix = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' +
                [Guid]::NewGuid().ToString('N').Substring(0, 8)
            $backup = "$targetDll.$suffix.bak"
        }
        Copy-Item -LiteralPath $targetDll -Destination $backup
        Write-Host "Backed up previous DLL: $backup"
    }
}
Copy-Item -LiteralPath $sourceDll -Destination $targetDll -Force
if (-not (Test-Path -LiteralPath $targetConfig)) {
    Copy-Item -LiteralPath $sourceConfig -Destination $targetConfig
    Write-Host "Installed sample config: $targetConfig"
}
else {
    Write-Host "Preserved existing config: $targetConfig"
}
Write-Host "Installed: $targetDll"
