param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory,
    [Parameter(Mandatory = $true)]
    [string] $RuntimeArchivePath
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$archivePath = (Resolve-Path -LiteralPath $RuntimeArchivePath).Path
$exe = Join-Path $game 'GTAIV.exe'
$probe = Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll'
$expectedArchiveHash = '5669E4423F93BEDFB0AE34579E922213775B46BBEE4DB6ADC953CB53E7AD9058'
$runtimeFiles = @('aCompleteEditionHook.asi', 'ScriptHook.dll', 'ScriptHookDotNet.asi')

if (-not (Test-Path -LiteralPath $exe)) { throw "GTAIV.exe not found in $game" }
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '1.2.0.59') {
    throw 'This T-001 package is only prepared for GTAIV.exe 1.2.0.59.'
}
if (-not (Test-Path -LiteralPath $probe)) { throw "Build the probe first: $probe" }
if ((Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -ne $expectedArchiveHash) {
    throw 'Runtime archive SHA256 differs from the verified Tomasak release package.'
}

$runningGame = Get-Process GTAIV -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }
if ($runningGame) { throw 'GTA IV is running. Close it before deploying T-001.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    foreach ($name in $runtimeFiles) {
        $entry = $archive.GetEntry($name)
        if ($null -eq $entry) { throw "Runtime archive is missing $name" }
        $target = Join-Path $game $name
        if (Test-Path -LiteralPath $target) {
            $sourceStream = $entry.Open()
            try {
                $sha = [Security.Cryptography.SHA256]::Create()
                try { $sourceHash = [BitConverter]::ToString($sha.ComputeHash($sourceStream)).Replace('-', '') }
                finally { $sha.Dispose() }
            }
            finally { $sourceStream.Dispose() }
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $sourceHash) {
                throw "$target already exists with different contents; inspect it before installing."
            }
        }
    }

    $scriptsDirectory = Join-Path $game 'scripts'
    $targetProbe = Join-Path $scriptsDirectory 'LibertyFramework.net.dll'
    if (Test-Path -LiteralPath $targetProbe) {
        if ((Get-FileHash -LiteralPath $targetProbe -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $probe -Algorithm SHA256).Hash) {
            throw "$targetProbe already exists with different contents; inspect it before installing."
        }
    }

    foreach ($name in $runtimeFiles) {
        $target = Join-Path $game $name
        if (-not (Test-Path -LiteralPath $target)) {
            $sourceStream = $archive.GetEntry($name).Open()
            try {
                $targetStream = [IO.File]::Open($target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
                try { $sourceStream.CopyTo($targetStream) }
                finally { $targetStream.Dispose() }
            }
            finally { $sourceStream.Dispose() }
        }
        Write-Host "Ready: $target"
    }

    New-Item -ItemType Directory -Force -Path $scriptsDirectory | Out-Null
    if (-not (Test-Path -LiteralPath $targetProbe)) {
        Copy-Item -LiteralPath $probe -Destination $targetProbe
    }
    Write-Host "Ready: $targetProbe"
}
finally {
    $archive.Dispose()
}
