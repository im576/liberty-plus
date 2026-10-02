Import-Module (Join-Path $script:RepoRoot 'tools/mood/MoodReceipt.psm1') -Force
$moodGame = Join-Path $script:Scratch 'mood-game'
$moodData = Join-Path $moodGame 'update/pc/data'
$moodRoot = Join-Path $moodGame 'scripts/LibertyFramework'
$moodBackup = Join-Path $moodRoot 'backups/mood-fixture'
New-Item -ItemType Directory -Force -Path $moodData,$moodBackup | Out-Null
$moodConfig = Join-Path $moodGame 'mood.json'
[IO.File]::WriteAllText($moodConfig, '{}')
$moodRows = @(foreach ($name in @('timecyc.dat','timecycext.dat')) {
    [IO.File]::WriteAllText((Join-Path $moodData $name), "fixture $name")
    Copy-Item -LiteralPath (Join-Path $moodData $name) -Destination (Join-Path $moodBackup $name)
    @{name=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $moodData $name)).Hash}
})
$moodReceipt = @{mode='citywide-mood';configSha256=(Get-FileHash -LiteralPath $moodConfig).Hash;backup=$moodBackup;installedFiles=$moodRows;previousFiles=$moodRows}
$moodReceiptPath = Join-Path $moodRoot 'installed-mood.json'
function Save-MoodFixture($receipt) { $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $moodReceiptPath }
function Test-MoodRejected($receipt, [string] $purpose, [string] $label) {
    Save-MoodFixture $receipt
    $rejected = $false
    try { Read-VerifiedMoodReceipt -GameDirectory $moodGame -Purpose $purpose -ConfigPath $moodConfig | Out-Null }
    catch { $rejected = $true }
    Test-That $label $rejected
}
Save-MoodFixture $moodReceipt
Test-That 'mood capture accepts both exact installed files and config' ([bool](Read-VerifiedMoodReceipt $moodGame Capture $moodConfig))
Test-That 'mood rollback accepts both exact backup files' ([bool](Read-VerifiedMoodReceipt $moodGame Rollback))
foreach ($purpose in @('Capture','Rollback')) {
    $field = if ($purpose -eq 'Capture') { 'installedFiles' } else { 'previousFiles' }
    $bad = $moodReceipt.Clone(); $bad[$field] = @()
    Test-MoodRejected $bad $purpose "mood $purpose rejects empty evidence"
    $bad[$field] = @($moodRows[0])
    Test-MoodRejected $bad $purpose "mood $purpose rejects missing second file"
    $bad[$field] = @($moodRows[0],$moodRows[0])
    Test-MoodRejected $bad $purpose "mood $purpose rejects duplicate files"
    $bad[$field] = @($moodRows[0],@{name='../outside.dat';sha256=$moodRows[1].sha256})
    Test-MoodRejected $bad $purpose "mood $purpose rejects unexpected path"
    $bad[$field] = @($moodRows[0],@{name='timecycext.dat';sha256=('0' * 64)})
    Test-MoodRejected $bad $purpose "mood $purpose rejects changed bytes"
}
$bad = $moodReceipt.Clone(); $bad.configSha256 = '0' * 64
Test-MoodRejected $bad Capture 'mood capture rejects stale config'
$bad = $moodReceipt.Clone(); $bad.mode = 'fusionfix'
Test-MoodRejected $bad Capture 'mood capture rejects restored pristine mode'
$bad = $moodReceipt.Clone(); $bad.backup = $moodData
Test-MoodRejected $bad Rollback 'mood rollback rejects backup outside backup root'
# Validation is read-only even for failed receipts.
Test-That 'mood validation leaves game fixture bytes unchanged' ((Get-FileHash -LiteralPath (Join-Path $moodData 'timecyc.dat')).Hash -eq $moodRows[0].sha256 -and (Get-FileHash -LiteralPath (Join-Path $moodData 'timecycext.dat')).Hash -eq $moodRows[1].sha256)
