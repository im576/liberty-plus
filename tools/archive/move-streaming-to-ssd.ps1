param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [string] $SsdRoot = 'C:\Games\GTAIV-SSD',
    [string[]] $Folders = @('pc\data', 'pc\models', 'pc\anim', 'pc\textures'),
    [double] $MinimumFreeGigabytesAfter = 15
)

# T-026: puts the folders GTA IV streams from while driving on the SSD, without touching Steam's library config.
# Each folder is copied, verified (file count and total bytes), the original is renamed to "<name>.hdd-backup", and a
# directory junction is created at the original path pointing to the SSD copy. Steam, the game and our installers keep
# using the same path. Undo: tools/restore-streaming-from-ssd.ps1.
$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }

function Measure-Tree([string]$path) {
    $files = Get-ChildItem -LiteralPath $path -Recurse -File -Force
    return [pscustomobject]@{ Count = @($files).Count; Bytes = ($files | Measure-Object Length -Sum).Sum }
}

$needed = 0
foreach ($folder in $Folders) {
    $source = Join-Path $game $folder
    if ((Get-Item -LiteralPath $source).Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
    $needed += (Measure-Tree $source).Bytes
}
$drive = (Split-Path -Qualifier $SsdRoot).TrimEnd(':')
$free = (Get-Volume -DriveLetter $drive).SizeRemaining
if (($free - $needed) / 1GB -lt $MinimumFreeGigabytesAfter) {
    throw ("Not enough SSD space: {0:N1} GB free, {1:N1} GB needed, {2} GB must remain." -f ($free / 1GB), ($needed / 1GB), $MinimumFreeGigabytesAfter)
}

foreach ($folder in $Folders) {
    $source = Join-Path $game $folder
    $item = Get-Item -LiteralPath $source
    if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { Write-Host "  already moved: $folder"; continue }
    $target = Join-Path $SsdRoot $folder
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    & robocopy $source $target /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NP /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder (exit $LASTEXITCODE)" }
    $a = Measure-Tree $source; $b = Measure-Tree $target
    if ($a.Count -ne $b.Count -or $a.Bytes -ne $b.Bytes) { throw "Verification failed for $folder ($($a.Count)/$($a.Bytes) vs $($b.Count)/$($b.Bytes))" }
    $backup = $source + '.hdd-backup'
    Rename-Item -LiteralPath $source -NewName (Split-Path -Leaf $backup)
    New-Item -ItemType Junction -Path $source -Target $target | Out-Null
    Write-Host ("  moved {0}: {1} files, {2:N2} GB -> {3}" -f $folder, $b.Count, ($b.Bytes / 1GB), $target)
}
Write-Host "Streaming folders are on the SSD. HDD originals kept as *.hdd-backup until you remove them."
