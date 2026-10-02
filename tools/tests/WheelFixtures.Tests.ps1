Import-Module (Join-Path $script:RepoRoot 'tools/autopilot/WheelConfigFixture.psm1') -Force
Import-Module (Join-Path $script:RepoRoot 'tools/autopilot/WheelLatency.psm1') -Force
$wheelRoot = Join-Path $script:Scratch 'wheel-fixtures'
$wheelConfigDirectory = Join-Path $wheelRoot 'scripts/LibertyFramework/config'
New-Item -ItemType Directory -Force -Path $wheelConfigDirectory | Out-Null
$wheelPath = Join-Path $wheelConfigDirectory 'arsenal.json'
$wheelOriginal = [Text.Encoding]::UTF8.GetBytes("{`r`n  `"unrelated`": {`"budget`": 0.8},`r`n  `"weaponWheel`": {`"enabled`": false, `"keyboardKey`": `"Tab`", `"padButton`": `"Back`"}`r`n}`r`n")
[IO.File]::WriteAllBytes($wheelPath, $wheelOriginal)
$wheelState = @{ Running = $true; Enabled = $false; Refuse = $false }
$wheelCommand = {
    param($line)
    if ($wheelState.Refuse -and $line -like 'restart*') { return "$line => error injected restore failure" }
    switch ($line) {
        'modules' { return "modules => weapon-wheel$(if (-not $wheelState.Running) { '(off: stopped)' }) avg=0.000 max=0.00 int=0" }
        'restart weapon-wheel' { $wheelState.Running = $true; $wheelState.Enabled = [bool](Get-Content $wheelPath -Raw | ConvertFrom-Json).weaponWheel.enabled; return "$line => restarted weapon-wheel" }
        'stop weapon-wheel' { $wheelState.Running = $false; return "$line => stopped weapon-wheel" }
        'wheel status' { return "wheel status => weapon_wheel_status open=False enabled=$($wheelState.Enabled)" }
        default { throw "unexpected test command $line" }
    }
}.GetNewClosure()

$fixture = New-WheelConfigFixture $wheelRoot $wheelRoot $wheelCommand
try {
    Set-WheelConfigFixture $fixture 'enabled'
    $modified = Get-Content $wheelPath -Raw | ConvertFrom-Json
    Test-That 'wheel fixture enables only the wheel and preserves unrelated tuning' ($modified.weaponWheel.enabled -and $modified.unrelated.budget -eq 0.8 -and $modified.weaponWheel.keyboardKey -eq 'Tab')
    Set-WheelConfigFixture $fixture 'invalid'
    $modified = Get-Content $wheelPath -Raw | ConvertFrom-Json
    Test-That 'wheel invalid fixture is parseable JSON with a known rejected navigation binding' (-not $modified.weaponWheel.enabled -and $modified.weaponWheel.keyboardKey -eq 'Enter')
}
finally { Restore-WheelConfigFixture $fixture $wheelCommand { $true } | Out-Null }
Test-That 'wheel fixture finally restores exact bytes and original enabled/running state' (
    [Convert]::ToBase64String([IO.File]::ReadAllBytes($wheelPath)) -ceq [Convert]::ToBase64String($wheelOriginal) -and $wheelState.Running -and -not $wheelState.Enabled -and $fixture.Restored)

$wheelState.Running = $false
$fixture = New-WheelConfigFixture $wheelRoot $wheelRoot $wheelCommand
try { Set-WheelConfigFixture $fixture 'enabled'; $wheelState.Running = $true }
finally { Restore-WheelConfigFixture $fixture $wheelCommand { $true } | Out-Null }
Test-That 'wheel fixture preserves a module that was originally stopped' (-not $wheelState.Running -and $fixture.Restored)

$wheelState.Running = $true
$fixture = New-WheelConfigFixture $wheelRoot $wheelRoot $wheelCommand
Set-WheelConfigFixture $fixture 'invalid'
$wheelState.Refuse = $true; $refused = $false
try { Restore-WheelConfigFixture $fixture $wheelCommand { $true } | Out-Null }
catch { $refused = $_.Exception.Message -match 'refused' }
$wheelState.Refuse = $false
Test-That 'restore command refusal is reported after restoring bytes, never a false success' ($refused -and -not $fixture.Restored -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($wheelPath)) -ceq [Convert]::ToBase64String($wheelOriginal))

$fixture = New-WheelConfigFixture $wheelRoot $wheelRoot $wheelCommand
Set-WheelConfigFixture $fixture 'invalid'; $exited = $false
try { Restore-WheelConfigFixture $fixture $wheelCommand { $false } | Out-Null }
catch { $exited = $_.Exception.Message -match 'game exit' }
Test-That 'game exit still restores config bytes but leaves live-state restoration unproven' ($exited -and -not $fixture.Restored -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($wheelPath)) -ceq [Convert]::ToBase64String($wheelOriginal))

$accepted = '[INFO] weapon_wheel_config enabled=True'
Test-That 'one accepted watcher callback satisfies the single-owner check' ((Test-WheelFixtureReload @($accepted) 'single') -match 'accepted=1')
$duplicate = $false; try { Test-WheelFixtureReload @($accepted,$accepted) 'single' | Out-Null } catch { $duplicate = $true }
Test-That 'duplicate accepted watcher callbacks fail' $duplicate
$stopped = $false; try { Test-WheelFixtureReload @($accepted) 'quiet' | Out-Null } catch { $stopped = $true }
Test-That 'a stopped-owner callback fails the quiet interval' $stopped
$rejected = $false; try { Test-WheelFixtureReload @('[ERROR] weapon_wheel_config_rejected keeping previous') 'single' | Out-Null } catch { $rejected = $true }
Test-That 'a rejection cannot masquerade as successful reload' $rejected

$goodLatency = @('[INFO] weapon_wheel_open frame=4 held=7','[INFO] weapon_wheel_first_draw frames=1 ms=10',
    '[INFO] weapon_wheel_open frame=9 held=7','[INFO] weapon_wheel_first_draw frames=0 ms=4')
Test-That 'all later openings can pass at zero or one frame' ((Test-WheelFirstDraw $goodLatency) -match 'openings=2 draws=2 max_frames=1 target=1')
$late = $false; try { Test-WheelFirstDraw ($goodLatency + @('[INFO] weapon_wheel_open frame=12 held=7','[INFO] weapon_wheel_first_draw frames=2 ms=359')) | Out-Null } catch { $late = $_.Exception.Message -match 'frames=2 ms=359' }
Test-That 'a later two-frame opening fails despite an initial passing opening' $late
$missing = $false; try { Test-WheelFirstDraw @('[INFO] weapon_wheel_open frame=4 held=7') | Out-Null } catch { $missing = $true }
Test-That 'a missing first-draw measurement fails' $missing
$orphan = $false; try { Test-WheelFirstDraw @('[INFO] weapon_wheel_first_draw frames=1 ms=10') | Out-Null } catch { $orphan = $true }
Test-That 'a first draw without an opening fails' $orphan
$echo = $false; try { Test-WheelFirstDraw @('[INFO] command line="wheel open" reply="weapon_wheel_open frame=4 held=7"') | Out-Null } catch { $echo = $true }
Test-That 'a command echo is not first-draw evidence' $echo

# The actual scenario runner must run the fixture's finally on fail-fast, without touching a real game.
$simModule = Join-Path $script:TestRoot 'SimulatedGame.psm1'
Import-Module $simModule -Force
$simState = Join-Path $wheelRoot 'sim-state'
Initialize-SimulatedGame $simState @{ commands = @{
    'modules' = @{ reply = 'weapon-wheel avg=0.000 max=0.00 int=0' }
    'restart weapon-wheel' = @{ reply = 'restarted weapon-wheel' }
    'wheel status' = @{ reply = 'weapon_wheel_status open=False enabled=False' }
} } @()
$scenario = Join-Path $wheelRoot 'fixture-fail-fast.txt'
[IO.File]::WriteAllLines($scenario, @('wheel-config begin','wheel-config invalid','unknown-command','wheel-config enabled'))
$output = & (Join-Path $script:RepoRoot 'tools/autopilot/Run-Scenario.ps1') -GameDirectory $wheelRoot -Scenario $scenario -OutputDirectory $wheelRoot -AutopilotModule $simModule -StopOnFailure 6>&1 | Out-String
$read = Read-ScenarioResult $output
$result = Get-Content $read.Path -Raw | ConvertFrom-Json
Test-That 'runner fail-fast restores fixture and preserves original failure' ($result.status -eq 'FAIL' -and $result.steps -eq 3 -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($wheelPath)) -ceq [Convert]::ToBase64String($wheelOriginal)) $output
Test-That 'runner cleanup has a separate successful restoration receipt' (Test-Path -LiteralPath (Join-Path (Split-Path $read.Path) 'wheel-config-restored.txt'))
