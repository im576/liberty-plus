param([Parameter(Mandatory = $true)][string] $GameDirectory)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$mock = [IO.Path]::GetFullPath((Join-Path $repoRoot 'staging\phase2-install-test'))
if (-not $mock.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test directory.' }
if (Test-Path -LiteralPath $mock) { throw "Test directory already exists: $mock" }
New-Item -ItemType Directory -Force -Path $mock | Out-Null

function Copy-GameFile([string] $relative) {
    $target = Join-Path $mock $relative
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath (Join-Path $game $relative) -Destination $target
}

foreach ($relative in @('GTAIV.exe', 'ScriptHookDotNet.asi', 'dinput8.dll', 'plugins\GTAIV.EFLC.FusionFix.asi')) {
    Copy-GameFile $relative
}
$manifest = Get-Content -LiteralPath (Join-Path $repoRoot 'staging\phase2\manifest.json') -Raw | ConvertFrom-Json
foreach ($file in $manifest.files) {
    if ($file.baseSha256) { Copy-GameFile $file.path }
}

& (Join-Path $PSScriptRoot 'install-phase2.ps1') -GameDirectory $mock
foreach ($file in $manifest.files) {
    $target = Join-Path $mock $file.path
    $expected = if ($file.policy -eq 'keep-existing' -and $file.baseSha256) { $file.baseSha256 } else { $file.sha256 }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $expected) { throw "Install test mismatch: $($file.path)" }
}

& (Join-Path $PSScriptRoot 'rollback-phase2.ps1') -GameDirectory $mock
foreach ($file in $manifest.files) {
    $target = Join-Path $mock $file.path
    if ($file.baseSha256) {
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $file.baseSha256) { throw "Rollback test mismatch: $($file.path)" }
    }
    elseif (Test-Path -LiteralPath $target) { throw "Rollback left created file: $($file.path)" }
}
Write-Host 'PHASE2_INSTALL_ROLLBACK_DRY_RUN_PASS'
