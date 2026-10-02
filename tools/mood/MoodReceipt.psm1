# Shared, read-only validation before capture or rollback. An empty file list proves nothing.
function Read-VerifiedMoodReceipt {
    param(
        [Parameter(Mandatory=$true)][string] $GameDirectory,
        [Parameter(Mandatory=$true)][ValidateSet('Capture','Rollback')][string] $Purpose,
        [string] $ConfigPath
    )
    $receiptPath = Join-Path $GameDirectory 'scripts/LibertyFramework/installed-mood.json'
    $receipt = Get-Content -LiteralPath $receiptPath -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
    if ($receipt.mode -notin @('citywide-mood','fusionfix')) { throw 'Unrecognized installed mood receipt mode.' }
    $expected = @('timecyc.dat','timecycext.dat')
    if ($Purpose -eq 'Capture') {
        if (-not $ConfigPath -or $receipt.mode -ne 'citywide-mood' -or
            $receipt.configSha256 -ne (Get-FileHash -LiteralPath $ConfigPath -ErrorAction Stop).Hash) {
            throw 'Installed mood candidate does not match this configuration.'
        }
        $rows = @($receipt.installedFiles)
        $directory = Join-Path $GameDirectory 'update/pc/data'
    } else {
        $rows = @($receipt.previousFiles)
        if (-not $receipt.backup) { throw 'Missing mood backup directory.' }
        $directory = [IO.Path]::GetFullPath($receipt.backup)
        $backupRoot = [IO.Path]::GetFullPath((Join-Path $GameDirectory 'scripts/LibertyFramework/backups'))
        if ((Split-Path -Parent $directory) -ine $backupRoot -or (Split-Path -Leaf $directory) -notlike 'mood-*') {
            throw 'Mood backup directory is outside the expected backup location.'
        }
    }
    if ($rows.Count -ne $expected.Count) { throw 'Mood receipt must contain both timecycle files.' }
    $seen = @{}
    foreach ($row in $rows) {
        if ($row.name -notin $expected -or $seen.ContainsKey([string]$row.name) -or $row.sha256 -notmatch '^[a-fA-F0-9]{64}$') {
            throw 'Mood receipt contains an invalid, duplicate or unexpected file.'
        }
        $seen[$row.name] = $true
        if ((Get-FileHash -LiteralPath (Join-Path $directory $row.name) -ErrorAction Stop).Hash -ne $row.sha256) {
            throw "Mood $Purpose hash mismatch: $($row.name)"
        }
    }
    return $receipt
}

Export-ModuleMember -Function Read-VerifiedMoodReceipt
