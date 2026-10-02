param(
    [Parameter(Mandatory=$true)][string] $GameDirectory,
    [Parameter(Mandatory=$true)][string] $OutputDirectory,
    # Pass only after the human authorizes closing the currently open game.
    [switch] $RestartRunningGame,
    [ValidateRange(1,3)][int] $StartupAttempts = 3,
    [ValidateRange(1,9)][int] $StartupMinutes = 9
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Import-Module (Join-Path $repo 'tools\local\GameLock.psm1') -Force
Import-Module (Join-Path $repo 'tools\autopilot\Autopilot.psm1') -Force 3>$null
Set-AutopilotGame $GameDirectory
$lock = Enter-GameLock "T-059 integrated citywide review ($repo)" -TimeoutMinutes 0
$installed = $false
$success = $false
$position = $null
$deadline = [DateTime]::UtcNow.AddMinutes(15)
try {
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $process = Get-GameProcess
    if ($process) {
        if (-not $RestartRunningGame) { throw 'GTA IV is open; human authorization is required before restarting this session.' }
        Stop-Game
    }
    $build = Read-InstalledBuild $GameDirectory
    if (-not $build -or [IO.Path]::GetFullPath($build.repo) -ine [IO.Path]::GetFullPath($repo)) { throw 'Installed gameplay build is from another worktree; no installation performed.' }
    $build | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'gameplay-build.json')
    Get-GamePluginInventory $GameDirectory | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'plugins.json')
    & (Join-Path $repo 'tools\install-mood.ps1') -GameDirectory $GameDirectory
    $installed = $true
    Copy-Item -LiteralPath (Join-Path $GameDirectory 'scripts\LibertyFramework\installed-mood.json') -Destination (Join-Path $OutputDirectory 'installed-mood.json')
    Start-GameReady -Attempts $StartupAttempts -DeadlineUtc ([DateTime]::UtcNow.AddMinutes($StartupMinutes)) -TelemetryPath (Join-Path $OutputDirectory 'startup.jsonl') | Out-Null
    Start-Sleep -Seconds 12
    $before = @(Invoke-EngineCommand @('alive','pos','arsenal status','storage state','wheel status','owned autopilot'))
    $before | Set-Content (Join-Path $OutputDirectory 'before.txt')
    if (($before -join ' ') -match '=> (error|unknown command)' -or ($before -join ' ') -notmatch 'player alive' -or ($before -join ' ') -notmatch 'storage_state open=False' -or ($before -join ' ') -notmatch 'weapon_wheel_status open=False' -or ($before -join ' ') -notmatch 'autopilot: nothing') { throw 'Unexpected active gameplay state; refusing capture setup.' }
    $posLine = @($before | Where-Object { $_ -match '^pos => ' })[0]
    if (-not $posLine) { throw 'Missing original position receipt.' }
    $position = (($posLine -replace '^pos => ','') -replace ' heading ',' ' -split ' core_')[0]
    $reply = @(& (Join-Path $repo 'tools\autopilot\Run-Scenario.ps1') -GameDirectory $GameDirectory -Scenario (Join-Path $repo 'tools\autopilot\scenarios\citywide-environment.txt') -OutputDirectory $OutputDirectory -ScenarioDeadlineUtc $deadline -StopOnFailure)
    $reply | ForEach-Object { Write-Host $_ }
    $resultLine = @($reply | Where-Object { $_ -like 'AUTOPILOT_RESULT *' })[-1]
    if (-not $resultLine) { throw 'Runtime batch produced no result receipt.' }
    $result = Get-Content -LiteralPath ($resultLine -replace '^AUTOPILOT_RESULT ','') -Raw | ConvertFrom-Json
    if ($result.status -ne 'PASS' -or @($result.screenshots).Count -ne 20) { throw "Runtime review failed: $($result.status), screenshots=$(@($result.screenshots).Count)" }
    $success = $true
} catch {
    $_ | Out-String | Set-Content (Join-Path $OutputDirectory 'review-error.txt')
    throw
} finally {
    try {
        if ($position) {
            if (-not (Get-GameProcess)) { throw 'Game exited before capture cleanup could restore the player state.' }
            $cleanup = @(Invoke-EngineCommand @('cam off','clear','hud on','restart autopilot',"tp $position",'owned autopilot','pos'))
            $cleanup | Set-Content (Join-Path $OutputDirectory 'cleanup.txt')
            if (($cleanup -join ' ') -match '=> (error|unknown command)' -or ($cleanup -join ' ') -notmatch 'autopilot: nothing') { $success = $false; throw 'Capture cleanup failed; see cleanup.txt.' }
            # Teleport completes asynchronously; the immediate command reply can show the old location.
            Start-Sleep -Seconds 2
            $settled = @(Invoke-EngineCommand @('alive','pos','owned autopilot','storage state','wheel status','arsenal status'))
            $settled | Set-Content (Join-Path $OutputDirectory 'settled-cleanup.txt')
            $actualPos = @($settled | Where-Object { $_ -match '^pos => ' })[0]
            $expectedCoordinates = $position -split ' '
            $actualCoordinates = ($actualPos -replace '^pos => ','') -split ' '
            if ($actualCoordinates.Count -lt 3) { throw 'Missing settled player position.' }
            for ($axis = 0; $axis -lt 3; $axis++) {
                $expectedAxis = [double]::Parse($expectedCoordinates[$axis], [Globalization.CultureInfo]::InvariantCulture)
                $actualAxis = [double]::Parse($actualCoordinates[$axis], [Globalization.CultureInfo]::InvariantCulture)
                if ([Math]::Abs($expectedAxis - $actualAxis) -gt 1.0) { throw 'Settled player position differs from the pre-capture location.' }
            }
            if (($settled -join ' ') -notmatch 'player alive' -or ($settled -join ' ') -notmatch 'autopilot: nothing' -or ($settled -join ' ') -notmatch 'storage_state open=False' -or ($settled -join ' ') -notmatch 'weapon_wheel_status open=False') { throw 'Unexpected settled gameplay state after capture.' }
        }
    } catch {
        $success = $false
        $_ | Out-String | Set-Content (Join-Path $OutputDirectory 'cleanup-error.txt')
        throw
    } finally {
        try {
            if ($installed -and -not $success) {
                Stop-Game
                & (Join-Path $repo 'tools\install-mood.ps1') -GameDirectory $GameDirectory -Rollback
            }
        } finally { Exit-GameLock $lock }
    }
}
Write-Host 'Twenty citywide candidate captures ready for visual inspection; cleanup verified and candidate remains installed.'
