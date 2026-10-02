# Run the real wrapper with disposable disk fixtures and simulated game boundaries.
# No production Autopilot module, game process, installation or machine lock is used.
$cityRoot = Join-Path $script:Scratch 'citywide-wrapper'
New-Item -ItemType Directory -Force -Path (Join-Path $cityRoot 'tools/mood'), (Join-Path $cityRoot 'tools/local'), (Join-Path $cityRoot 'tools/autopilot') | Out-Null
Copy-Item -LiteralPath (Join-Path $script:RepoRoot 'tools/mood/Run-CitywideReview.ps1') -Destination (Join-Path $cityRoot 'tools/mood/Run-CitywideReview.ps1')
@'
function Enter-GameLock { return $true }
function Exit-GameLock { param($Lock) [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'unlocked'), 'yes') }
function Read-InstalledBuild { return @{repo=$env:LIBERTY_CITY_FIXTURE} }
function Get-GamePluginInventory { return @() }
Export-ModuleMember -Function *
'@ | Set-Content (Join-Path $cityRoot 'tools/local/GameLock.psm1')
@'
function Set-AutopilotGame { }
function Get-GameProcess { return (Test-Path (Join-Path $env:LIBERTY_CITY_FIXTURE 'ready')) -and -not (Test-Path (Join-Path $env:LIBERTY_CITY_FIXTURE 'exited')) }
function Start-GameReady { [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'ready'), 'yes') }
function Stop-Game { [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'stopped'), 'yes') }
function Start-Sleep { }
function Invoke-EngineCommand {
    param([string[]] $Lines)
    $position = '1 2 3'
    if ($Lines -contains 'cam off') { [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'cleaned'), 'yes') }
    if ($env:LIBERTY_CITY_CASE -eq 'bad-position' -and (Test-Path (Join-Path $env:LIBERTY_CITY_FIXTURE 'cleaned'))) { $position = '9 9 9' }
    return @('alive => player alive health=100',"pos => $position heading 0 core_peds=0",'storage state => storage_state open=False','wheel status => weapon_wheel_status open=False','owned autopilot => autopilot: nothing')
}
Export-ModuleMember -Function *
'@ | Set-Content (Join-Path $cityRoot 'tools/autopilot/Autopilot.psm1')
@'
param($GameDirectory,[switch] $Rollback)
if ($Rollback) { [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'rolled-back'),'yes'); return }
$root = Join-Path $GameDirectory 'scripts/LibertyFramework'
New-Item -ItemType Directory -Force $root | Out-Null
'{}' | Set-Content (Join-Path $root 'installed-mood.json')
'@ | Set-Content (Join-Path $cityRoot 'tools/install-mood.ps1')
@'
param($GameDirectory,$Scenario,$OutputDirectory,$ScenarioDeadlineUtc,[switch]$StopOnFailure)
$path = Join-Path $OutputDirectory 'result.json'
@{status='PASS';screenshots=@(1..20)} | ConvertTo-Json | Set-Content $path
if ($env:LIBERTY_CITY_CASE -eq 'exited') { [IO.File]::WriteAllText((Join-Path $env:LIBERTY_CITY_FIXTURE 'exited'),'yes') }
Write-Output "AUTOPILOT_RESULT $path"
'@ | Set-Content (Join-Path $cityRoot 'tools/autopilot/Run-Scenario.ps1')
$cityOldFixture = $env:LIBERTY_CITY_FIXTURE; $cityOldCase = $env:LIBERTY_CITY_CASE
try {
    $env:LIBERTY_CITY_FIXTURE = $cityRoot
    foreach ($case in @('success','exited','bad-position')) {
        foreach ($marker in @('ready','exited','stopped','cleaned','rolled-back','unlocked')) { [IO.File]::Delete((Join-Path $cityRoot $marker)) }
        $env:LIBERTY_CITY_CASE = $case
        $cityOutput = Join-Path $cityRoot ('reports/' + $case)
        $cityShell = (Get-Process -Id $PID).Path
        $cityOldPreference = $ErrorActionPreference
        try {
            # Windows PowerShell promotes child stderr to NativeCommandError; these two failures are intentional.
            $ErrorActionPreference = 'Continue'
            $cityText = & $cityShell -NoProfile -File (Join-Path $cityRoot 'tools/mood/Run-CitywideReview.ps1') -GameDirectory $cityRoot -OutputDirectory $cityOutput 2>&1 | Out-String
            $cityExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = $cityOldPreference }
        $cityText | Set-Content (Join-Path $cityRoot ($case + '.log'))
        Test-That "citywide $case releases its lock" (Test-Path (Join-Path $cityRoot 'unlocked'))
        if ($case -eq 'success') {
            Test-That 'citywide settled success retains candidate for review' ($cityExit -eq 0 -and -not (Test-Path (Join-Path $cityRoot 'rolled-back'))) $cityText
        } else {
            Test-That "citywide $case fails and rolls back even after PASS captures" ($cityExit -ne 0 -and (Test-Path (Join-Path $cityRoot 'rolled-back'))) $cityText
            Test-That "citywide $case retains cleanup failure evidence" (Test-Path (Join-Path $cityOutput 'cleanup-error.txt'))
        }
    }
} finally {
    $env:LIBERTY_CITY_FIXTURE = $cityOldFixture; $env:LIBERTY_CITY_CASE = $cityOldCase
}
