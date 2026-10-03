param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [switch] $Restore,
    [switch] $Rollback
)

# M-1 Liberty Mood: generates timecyc.dat / timecycext.dat from validated FusionFix pristine copies
# (*.fusionfix next to them) and config/mood.json, then installs them into update\pc\data. Re-running always starts
# from the pristine copies, so tuning never compounds. -Restore puts FusionFix's files back;
# -Rollback restores the exact pre-install files recorded in installed-mood.json.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot 'local\GameLock.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'mood\MoodReceipt.psm1') -Force
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$gameLock = Enter-GameLock "Liberty Mood ($repoRoot)" -TimeoutMinutes 0
$work = $null
try {
if ($Restore -and $Rollback) { throw 'Choose Restore or Rollback, not both.' }
if (Get-Process -Name GTAIV -ErrorAction SilentlyContinue) { throw 'GTA IV is running. Close it first.' }
$data = Join-Path $game 'update\pc\data'
$files = @('timecyc.dat', 'timecycext.dat')
$receiptPath = Join-Path $game 'scripts\LibertyFramework\installed-mood.json'
if ($Rollback) {
    $receipt = Read-VerifiedMoodReceipt -GameDirectory $game -Purpose Rollback
    foreach ($row in $receipt.previousFiles) {
        Copy-Item -LiteralPath (Join-Path $receipt.backup $row.name) -Destination (Join-Path $data $row.name) -Force
        if ((Get-FileHash -LiteralPath (Join-Path $data $row.name)).Hash -ne $row.sha256) { throw "Rollback verification failed: $($row.name)" }
    }
    $oldReceipt = Join-Path $receipt.backup 'installed-mood.json'
    if (Test-Path -LiteralPath $oldReceipt) { Copy-Item -LiteralPath $oldReceipt -Destination $receiptPath -Force }
    else { Remove-Item -LiteralPath $receiptPath -Force }
    Write-Host 'Liberty Mood rolled back; exact previous file hashes verified.'
    return
}
foreach ($name in $files) {
    $pristine = Join-Path $data ($name + '.fusionfix')
    if (-not (Test-Path -LiteralPath $pristine)) { throw "Missing validated FusionFix source: $pristine" }
}
if (-not $Restore) {
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
$tool = Join-Path $repoRoot 'tools\mood\bin\MoodTimecycle.exe'
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $tool) | Out-Null
& $csc /nologo /warn:4 /warnaserror+ /target:exe "/out:$tool" /reference:System.Web.Extensions.dll (Join-Path $repoRoot 'tools\mood\MoodTimecycle.cs')
if ($LASTEXITCODE -ne 0) { throw 'MoodTimecycle build failed' }
}
$work = Join-Path ([IO.Path]::GetTempPath()) ('lf-mood-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null
try {
    if ($Restore) {
        foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $data ($name + '.fusionfix')) -Destination (Join-Path $work $name) }
    } else {
        & $tool (Join-Path $repoRoot 'config\mood.json') (Join-Path $data 'timecyc.dat.fusionfix') (Join-Path $data 'timecycext.dat.fusionfix') (Join-Path $work 'timecyc.dat') (Join-Path $work 'timecycext.dat')
        if ($LASTEXITCODE -ne 0) { throw 'MoodTimecycle failed' }
    }
    $backup = Join-Path $game ('scripts\LibertyFramework\backups\mood-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $backup -Force | Out-Null
    $previous = @($files | ForEach-Object {
        Copy-Item -LiteralPath (Join-Path $data $_) -Destination (Join-Path $backup $_)
        [ordered]@{name=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $data $_)).Hash}
    })
    if (Test-Path -LiteralPath $receiptPath) { Copy-Item -LiteralPath $receiptPath -Destination (Join-Path $backup 'installed-mood.json') }
    try {
    foreach ($name in $files) {
        Copy-Item -LiteralPath (Join-Path $work $name) -Destination (Join-Path $data $name) -Force
        if ((Get-FileHash -LiteralPath (Join-Path $data $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $work $name)).Hash) { throw "Install hash mismatch: $name" }
        Write-Host "  installed $name"
    }
    $installed = @($files | ForEach-Object { [ordered]@{name=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $data $_)).Hash;sourceSha256=(Get-FileHash -LiteralPath (Join-Path $data ($_ + '.fusionfix'))).Hash} })
    [ordered]@{utc=[DateTime]::UtcNow.ToString('o');repo=$repoRoot;mode=$(if($Restore){'fusionfix'}else{'citywide-mood'});configSha256=(Get-FileHash -LiteralPath (Join-Path $repoRoot 'config\mood.json')).Hash;backup=$backup;previousFiles=$previous;installedFiles=$installed} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8
    } catch {
        $failure = $_
        Write-Warning "Mood installation failed; restoring previous files: $failure"
        foreach ($name in $files) { Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $data $name) -Force }
        if (Test-Path -LiteralPath (Join-Path $backup 'installed-mood.json')) { Copy-Item -LiteralPath (Join-Path $backup 'installed-mood.json') -Destination $receiptPath -Force }
        else { Remove-Item -LiteralPath $receiptPath -Force -ErrorAction SilentlyContinue }
        throw $failure
    }
    Write-Host 'Liberty Mood installed. FusionFix originals: update\pc\data\*.fusionfix (restore with -Restore).'
}
finally {
    $resolvedWork = [IO.Path]::GetFullPath($work)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/')
    if ((Split-Path -Parent $resolvedWork) -ine $tempRoot -or (Split-Path -Leaf $resolvedWork) -notlike 'lf-mood-*') { throw 'Mood scratch path escaped its temporary directory.' }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force -ErrorAction SilentlyContinue
}
} finally { Exit-GameLock $gameLock }
