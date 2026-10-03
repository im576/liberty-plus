param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $BackupDirectory
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$backup = (Resolve-Path -LiteralPath $BackupDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'Close GTA IV before rollback.' }
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $game 'scripts\LibertyFramework\backups')) + [IO.Path]::DirectorySeparatorChar
if (-not $backup.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup is outside the game backup directory.' }
$receipt = Get-Content -LiteralPath (Join-Path $backup 'rollback.json') -Raw | ConvertFrom-Json
if ($receipt.package -ne 'violent-liberty-companion') { throw 'Wrong rollback package.' }
foreach ($action in $receipt.actions) {
    $target = Join-Path $game $action.path
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        if ($action.hadOriginal) { throw "Missing installed target: $($action.path)" }
        continue
    }
    $currentHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($currentHash -ne $action.installedSha256 -and $currentHash -ne $action.originalSha256) { throw "Target changed since install: $($action.path)" }
}
foreach ($action in $receipt.actions) {
    $target = Join-Path $game $action.path
    if ($action.hadOriginal) {
        $saved = Join-Path (Join-Path $backup 'original') $action.path
        if ((Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash -ne $action.originalSha256) { throw "Backup changed: $($action.path)" }
        Copy-Item -LiteralPath $saved -Destination $target -Force
        if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $action.originalSha256) { throw "Restore hash mismatch: $($action.path)" }
    }
    elseif (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
    Write-Host "  restored $($action.path)"
}
Write-Host 'Violent Liberty companion rolled back.'
