param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $BackupDirectory
)

# Restores what install-weapon-pack.ps1 replaced; files it added are moved into the backup folder (never deleted).
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }
$manifest = Get-Content -LiteralPath (Join-Path $BackupDirectory 'manifest.json') -Raw | ConvertFrom-Json
foreach ($entry in $manifest) {
    $target = Join-Path $game $entry.Path
    if ($entry.Existed) { Copy-Item -LiteralPath (Join-Path $BackupDirectory $entry.Path) -Destination $target -Force; Write-Host "  restored $($entry.Path)" }
    elseif (Test-Path -LiteralPath $target) {
        $moved = Join-Path $BackupDirectory ($entry.Path + '.removed')
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $moved) | Out-Null
        Move-Item -LiteralPath $target -Destination $moved -Force
        Write-Host "  removed $($entry.Path)"
    }
}
Write-Host "Weapon pack rolled back from $BackupDirectory"
