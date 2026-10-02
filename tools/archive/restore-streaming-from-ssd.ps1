param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [string[]] $Folders = @('pc\data', 'pc\models', 'pc\anim', 'pc\textures')
)

# Undoes move-streaming-to-ssd.ps1: removes each junction and renames "<name>.hdd-backup" back. The SSD copies are left
# in place for the owner to delete. Files the game or installers changed after the move exist only on the SSD copy,
# so they are copied back over the backup first.
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }
foreach ($folder in $Folders) {
    $path = Join-Path $game $folder
    $backup = $path + '.hdd-backup'
    if (-not (Test-Path -LiteralPath $backup)) { Write-Host "  no backup: $folder"; continue }
    $item = Get-Item -LiteralPath $path
    if (-not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "$folder is not a junction" }
    $target = $item.Target
    if ($target -is [array]) { $target = $target[0] }
    & robocopy $target $backup /E /XO /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder (exit $LASTEXITCODE)" }
    [IO.Directory]::Delete($path)   # removes the junction only, never its target
    Rename-Item -LiteralPath $backup -NewName (Split-Path -Leaf $path)
    Write-Host "  restored $folder (SSD copy left at $target)"
}
