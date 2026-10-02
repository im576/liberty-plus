param(
    [string] $SteamGameFolder = 'D:\SteamLibrary\steamapps\common\Grand Theft Auto IV',
    [string] $SsdGameFolder = 'C:\Games\Grand Theft Auto IV',
    [string] $PartialCopy = 'C:\Grand Theft Auto IV',
    [string] $StreamingCopy = 'C:\Games\GTAIV-SSD'
)

# T-026: moves the whole game install to the SSD while Steam keeps its library path. The Steam folder on the HDD
# becomes a directory junction to the SSD copy, and the HDD original is kept as "<folder>.hdd-backup".
# Steps: undo the earlier per-folder streaming junctions, complete the owner's partial copy (identical files are
# skipped), verify file count and bytes, swap in the junction, then remove the redundant streaming copy.
$ErrorActionPreference = 'Stop'
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }
$game = Join-Path $SteamGameFolder 'GTAIV'

function Measure-Tree([string]$path) {
    $files = Get-ChildItem -LiteralPath $path -Recurse -File -Force | Where-Object { $_.FullName -notmatch '\.hdd-backup(\\|$)' }
    return [pscustomobject]@{ Count = @($files).Count; Bytes = ($files | Measure-Object Length -Sum).Sum }
}

# 1. Restore the per-folder junctions so the HDD tree is plain folders again (newer files copied back first).
foreach ($folder in 'pc\data', 'pc\models', 'pc\anim', 'pc\textures') {
    $path = Join-Path $game $folder
    $backup = $path + '.hdd-backup'
    if (-not (Test-Path -LiteralPath $backup)) { continue }
    $target = Join-Path $StreamingCopy $folder
    & robocopy $target $backup /E /XO /COPY:DAT /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy back failed for $folder" }
    [IO.Directory]::Delete($path)
    Rename-Item -LiteralPath $backup -NewName (Split-Path -Leaf $path)
    Write-Host "  unjunctioned $folder"
}

# 2. Put the partial copy at its final location (same drive: instant) and complete it from the HDD original.
if ((Test-Path -LiteralPath $PartialCopy) -and -not (Test-Path -LiteralPath $SsdGameFolder)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $SsdGameFolder) | Out-Null
    Move-Item -LiteralPath $PartialCopy -Destination $SsdGameFolder
    Write-Host "  moved partial copy to $SsdGameFolder"
}
& robocopy $SteamGameFolder $SsdGameFolder /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ /NFL /NDL /NP /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy completion failed (exit $LASTEXITCODE)" }
$a = Measure-Tree $SteamGameFolder; $b = Measure-Tree $SsdGameFolder
if ($a.Count -ne $b.Count -or $a.Bytes -ne $b.Bytes) { throw "Verification failed: HDD $($a.Count)/$($a.Bytes) vs SSD $($b.Count)/$($b.Bytes)" }
Write-Host ("  verified {0} files, {1:N2} GB" -f $b.Count, ($b.Bytes / 1GB))

# 3. Swap: HDD folder -> backup, junction in its place.
$backupFolder = $SteamGameFolder + '.hdd-backup'
Rename-Item -LiteralPath $SteamGameFolder -NewName (Split-Path -Leaf $backupFolder)
New-Item -ItemType Junction -Path $SteamGameFolder -Target $SsdGameFolder | Out-Null
Write-Host "  $SteamGameFolder -> $SsdGameFolder (HDD original kept at $backupFolder)"

# 4. The per-folder streaming copy is now redundant.
if (Test-Path -LiteralPath $StreamingCopy) {
    Remove-Item -LiteralPath $StreamingCopy -Recurse -Force
    Write-Host "  removed redundant $StreamingCopy"
}
Write-Host 'GTA IV now runs from the SSD.'
