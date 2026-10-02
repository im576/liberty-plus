# Tests of tools/verify-local.ps1 and tools/local/VerifyLocal.psm1 without a game: the real orchestration runs against
# simulated tools (small PowerShell commands), the simulated game (SimulatedGame.psm1) and a local bare Git remote.
$verifyLocal = Join-Path $script:RepoRoot (Join-Path 'tools' 'verify-local.ps1')
Import-Module (Join-Path $script:RepoRoot (Join-Path 'tools' (Join-Path 'local' 'VerifyLocal.psm1'))) -Force
Import-Module (Join-Path $script:TestRoot 'SimulatedGame.psm1') -Force

# ---- Pure helpers
Test-That 'argument: plain value unquoted' ((ConvertTo-CommandArgument 'selftest') -eq 'selftest')
Test-That 'argument: spaces quoted' ((ConvertTo-CommandArgument 'C:\Program Files\GTA IV') -eq '"C:\Program Files\GTA IV"')
Test-That 'argument: trailing backslash doubled inside quotes' ((ConvertTo-CommandArgument 'D:\Steam Library\') -eq '"D:\Steam Library\\"')
Test-That 'argument: embedded quote escaped' ((ConvertTo-CommandArgument 'say "hi"') -eq '"say \"hi\""')
Test-That 'argument: empty string kept' ((ConvertTo-CommandArgument '') -eq '""')
$json = '{ "status": "ok", "compiled": { "textureFormat": "DXT5", "textureLevels": 6, "textureQuality": { "psnrAlphaDb": 31.5 } } }' | ConvertFrom-Json
$expect = '{ "status": "ok", "compiled.textureFormat": "DXT5", "compiled.textureLevels": 6, "compiled.textureQuality.psnrAlphaDb": "present" }' | ConvertFrom-Json
Test-That 'json expectations: all match' (@(Test-JsonExpectations $json $expect).Count -eq 0)
$expect = '{ "compiled.textureLevels": 7, "compiled.missing": "present" }' | ConvertFrom-Json
Test-That 'json expectations: wrong value and missing field reported' (@(Test-JsonExpectations $json $expect).Count -eq 2)

# ---- The real queue: ordering and selection
$queue = Read-CheckQueue (Join-Path $script:RepoRoot (Join-Path 'tests' (Join-Path 'local' 'checks.json')))
$order = @(Select-Checks $queue @() @() $false $false | ForEach-Object { $_.id })
$first = { param($predicate) for ($i = 0; $i -lt $order.Count; $i++) { $c = $queue.checks | Where-Object { $_.id -eq $order[$i] }; if (& $predicate $c) { return $i } }; return -1 }
$last = { param($predicate) for ($i = $order.Count - 1; $i -ge 0; $i--) { $c = $queue.checks | Where-Object { $_.id -eq $order[$i] }; if (& $predicate $c) { return $i } }; return -1 }
$install = [array]::IndexOf($order, 'LOOP-package-install')
Test-That 'queue order: package-install after every offline tool and probe' ((& $last { param($c) ($c.kind -eq 'pc-offline' -and $c.run.tool -notin 'package-install', 'content-report') -or $c.kind -eq 'probe' }) -lt $install)
Test-That 'queue order: scenarios and content reports after package-install' ((& $first { param($c) $c.kind -eq 'scenario' -or $c.run.tool -eq 'content-report' }) -gt $install)
Test-That 'queue order: manual checks last' ((& $first { param($c) $c.kind -eq 'manual' }) -gt (& $last { param($c) $c.kind -ne 'manual' }))
Test-That 'queue order: the lock-free builds run before every check that reads the game' (
    (& $last { param($c) $c.kind -eq 'pc-offline' -and $c.run.tool -in 'build', 'content-selftest' }) -lt (& $first { param($c) -not ($c.kind -eq 'pc-offline' -and $c.run.tool -in 'build', 'content-selftest') }))
$smoke = @(Select-Checks $queue @() @() $true $false | ForEach-Object { $_.id })
Test-That 'smoke: build, package-install, SDK self-test' (($smoke -join ',') -eq 'LOOP-build,LOOP-package-install,SDK-selftest') ($smoke -join ',')
$only = @(Select-Checks $queue @('T027-raycast') @() $false $false | ForEach-Object { $_.id })
Test-That 'only a scenario: package-install is added before it' (($only -join ',') -eq 'LOOP-package-install,T027-raycast') ($only -join ',')
$threw = $false; try { Select-Checks $queue @('NO-such-check') @() $false $false | Out-Null } catch { $threw = $true }
Test-That 'only an unknown id: refused' $threw
# No active scenario needs a probe any more (T027-raycast-objects was retired 2026-09-30), so the needs rule is tested on a
# copy of the queue with that check active again.
$needsQueue = $queue | ConvertTo-Json -Depth 20 | ConvertFrom-Json
($needsQueue.checks | Where-Object { $_.id -eq 'T027-raycast-objects' }).status = 'QUEUED'
$only = @(Select-Checks $needsQueue @('T027-raycast-objects') @() $false $false | ForEach-Object { $_.id })
Test-That 'only a scenario that needs a probe: the probe and package-install run first' (($only -join ',') -eq 'PROBE-collision,LOOP-package-install,T027-raycast-objects') ($only -join ',')
$only = @(Select-Checks $needsQueue @() @('scenario') $false $false | ForEach-Object { $_.id })
Test-That 'kind scenario: a needed probe is still added' ($only -contains 'PROBE-collision') ($only -join ',')

# ---- A full simulated run
$root = Join-Path $script:Scratch 'verify-local'
New-Item -ItemType Directory -Force -Path (Join-Path $root 'scenarios') | Out-Null
$remote = Join-Path $root 'remote.git'
& git init -q --bare $remote
[IO.File]::WriteAllLines((Join-Path $root (Join-Path 'scenarios' 'good.txt')), @('selftest', 'expect "selftest_done passed=\d+ failed=0" 2', 'shot view'))
[IO.File]::WriteAllLines((Join-Path $root (Join-Path 'scenarios' 'crashy.txt')), @('god on', 'boom'))
# A scenario that spawns what a probe of the same run found ({probe:<id>:<field>}; the simulated probe reports "simulated").
[IO.File]::WriteAllLines((Join-Path $root (Join-Path 'scenarios' 'probed.txt')), @('spawnprop {probe:T-probe:probe} 3 0', 'expect "autopilot_prop handle=\d+ model=simulated" 2'))
foreach ($asset in 'good_asset', 'bad_asset') {
    $folder = Join-Path $root (Join-Path 'reports' (Join-Path 'content' $asset))
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
}
[IO.File]::WriteAllText((Join-Path $root 'reports/content/good_asset/report.json'), '{ "status": "ok", "compiled": { "textureFormat": "DXT1" } }')
[IO.File]::WriteAllText((Join-Path $root 'reports/content/bad_asset/report.json'), '{ "status": "error" }')

function New-TestQueue([object[]] $checks) {
    $q = @{ schemaVersion = 1; sessions = @(@{ id = 'play'; title = 'Play'; setup = 'none' }); checks = $checks }
    $path = Join-Path $root 'queue.json'
    [IO.File]::WriteAllText($path, ($q | ConvertTo-Json -Depth 8))
    return $path
}
function New-Check([string] $id, [string] $kind, $run, [hashtable] $extra) {
    $c = @{ id = $id; task = 'TEST'; title = $id; kind = $kind; run = $run; pass = 'x'; proves = 'x'; status = 'QUEUED' }
    if ($extra) { foreach ($k in $extra.Keys) { $c[$k] = $extra[$k] } }
    return $c
}
$checks = @(
    (New-Check 'T-build' 'pc-offline' @{ tool = 'build' }),
    (New-Check 'T-verify-fails' 'pc-offline' @{ tool = 'verify' }),
    (New-Check 'T-hangs' 'pc-offline' @{ tool = 'wtdcheck'; archives = @('x.img') }),
    (New-Check 'T-output-mismatch' 'pc-offline' @{ tool = 'content-selftest' }),
    (New-Check 'T-blender' 'pc-offline' @{ tool = 'blender-tests' }),
    (New-Check 'T-probe' 'probe' @{ tool = 'probe'; probe = 'drawables' }),
    (New-Check 'T-roundtrip' 'pc-offline' @{ tool = 'drawable-roundtrip' }),
    (New-Check 'T-roundtrip-fails' 'pc-offline' @{ tool = 'drawable-roundtrip'; archives = @('x.img') }),
    (New-Check 'T-probe-noreport' 'probe' @{ tool = 'probe'; probe = 'collision' }),
    (New-Check 'LOOP-package-install' 'pc-offline' @{ tool = 'package-install' }),
    (New-Check 'T-report-good' 'pc-offline' @{ tool = 'content-report'; asset = 'good_asset'; expect = @{ status = 'ok'; 'compiled.textureFormat' = 'DXT1' } }),
    (New-Check 'T-report-bad' 'pc-offline' @{ tool = 'content-report'; asset = 'bad_asset'; expect = @{ status = 'ok' } }),
    (New-Check 'T-scenario-good' 'scenario' @{ scenario = 'good' } @{ review = @{ screenshots = @{ view = 'anything' } } }),
    (New-Check 'T-scenario-crash' 'scenario' @{ scenario = 'crashy' }),
    (New-Check 'T-scenario-after-crash' 'scenario' @{ scenario = 'good' }),
    (New-Check 'T-scenario-probed' 'scenario' @{ scenario = 'probed' } @{ needs = @('T-probe') }),
    (New-Check 'T-manual-pass' 'manual' @{ steps = @('play') } @{ minutes = 1; session = 'play' }),
    (New-Check 'T-manual-skipped' 'manual' @{ steps = @('play') } @{ minutes = 1; session = 'play' })
)
$queuePath = New-TestQueue $checks
$sim = @{
    remote = $remote
    blender = ''
    scenarioTimeoutSeconds = 120
    tools = @{
        'T-build' = @{ exit = 0; output = 'Built everything' }
        'T-verify-fails' = @{ exit = 1; output = 'RESULT passed=3 failed=1' }
        'T-hangs' = @{ exit = 0; sleepSeconds = 30; timeoutSeconds = 2 }
        'T-output-mismatch' = @{ exit = 0; output = 'selftest: ok passed=5 failed=2'; pass = 'selftest: ok passed=\d+ failed=0' }
        'T-probe-noreport' = @{ exit = 0; writesReport = $false }
        'T-roundtrip' = @{ exit = 0; output = 'roundtrip: ok drawables=9 eligible=4 multi=2 identical=4 failed=0'; pass = 'roundtrip: ok' }
        'T-roundtrip-fails' = @{ exit = 1; output = 'roundtrip: FAILED drawables=9 eligible=4 multi=2 identical=3 failed=1'; pass = 'roundtrip: ok' }
    }
    manual = @{ 'T-manual-pass' = 'p' }
}
$simFile = Join-Path $root 'simulation.json'
[IO.File]::WriteAllText($simFile, ($sim | ConvertTo-Json -Depth 8))
Initialize-SimulatedGame (Join-Path $root 'game') @{
    knownCommands = @('god')
    commands = @{ 'selftest' = @{ reply = 'started'; log = @('selftest_done passed=3 failed=0') }; 'boom' = @{ reply = 'ok'; crash = $true }
        'spawnprop simulated 3 0' = @{ reply = 'spawning prop simulated'; log = @('autopilot_prop handle=77 model=simulated') } }
} @()
$worktreesBefore = @(& git -C $script:RepoRoot worktree list --porcelain | Where-Object { $_ -like 'worktree *' } | Sort-Object)
& $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -KeepInstall 6>&1 | Out-Null
$runFolder = Get-ChildItem -LiteralPath (Join-Path $root 'results') -Directory | Select-Object -First 1
$summary = Get-Content -LiteralPath (Join-Path $runFolder.FullName 'summary.json') -Raw | ConvertFrom-Json
$status = @{}; foreach ($c in $summary.checks) { $status[$c.id] = $c.status }
Test-That 'run: a passing tool is PASS' ($status['T-build'] -eq 'PASS') ($status | Out-String)
Test-That 'run: a failing tool is FAIL' ($status['T-verify-fails'] -eq 'FAIL')
Test-That 'run: a hung tool is killed and ERROR' ($status['T-hangs'] -eq 'ERROR')
Test-That 'run: exit 0 with output not matching the pass pattern is FAIL' ($status['T-output-mismatch'] -eq 'FAIL')
Test-That 'run: Blender not configured is NOT-RUN' ($status['T-blender'] -eq 'NOT-RUN')
Test-That 'run: a probe with a report needs review, never auto-PASS' ($status['T-probe'] -eq 'NEEDS-REVIEW')
Test-That 'run: a probe without a report is FAIL' ($status['T-probe-noreport'] -eq 'FAIL')
Test-That 'run: a drawable round trip that passes is PASS, with its report as evidence' ($status['T-roundtrip'] -eq 'PASS' -and
    (@($summary.checks | Where-Object { $_.id -eq 'T-roundtrip' })[0].evidence -contains 'T-roundtrip.json')) (@($summary.checks | Where-Object { $_.id -eq 'T-roundtrip' })[0] | ConvertTo-Json -Compress)
Test-That 'run: a drawable round trip with a failed file is FAIL' ($status['T-roundtrip-fails'] -eq 'FAIL')
Test-That 'run: package-install PASS' ($status['LOOP-package-install'] -eq 'PASS')
Test-That 'run: content report with matching fields is PASS' ($status['T-report-good'] -eq 'PASS')
Test-That 'run: content report with a wrong field is FAIL' ($status['T-report-bad'] -eq 'FAIL')
Test-That 'run: a passing scenario with screenshots to judge is NEEDS-REVIEW' ($status['T-scenario-good'] -eq 'NEEDS-REVIEW')
Test-That 'run: a crashing scenario is CRASH' ($status['T-scenario-crash'] -eq 'CRASH')
Test-That 'run: the scenario after a crash relaunches and passes' ($status['T-scenario-after-crash'] -eq 'PASS')
Test-That 'run: a scenario uses the value its probe found in the same run' ($status['T-scenario-probed'] -eq 'PASS') ($status['T-scenario-probed'])
Test-That 'run: manual answer p is PASS' ($status['T-manual-pass'] -eq 'PASS')
Test-That 'run: unanswered manual check is NOT-RUN' ($status['T-manual-skipped'] -eq 'NOT-RUN')
$scenarioEvidence = Join-Path $runFolder.FullName 'T-scenario-good'
$imageName = if ($env:OS -eq 'Windows_NT') { 'view.jpg' } else { 'view.png' }
$imagePath = Join-Path $scenarioEvidence $imageName
$scenarioResult = Get-Content -LiteralPath (Join-Path $scenarioEvidence 'result.json') -Raw | ConvertFrom-Json
Test-That 'run: evidence copied (report, image and matching result reference)' (
    (Test-Path -LiteralPath (Join-Path $scenarioEvidence 'report.md')) -and
    (Test-Path -LiteralPath $imagePath) -and (Get-Item -LiteralPath $imagePath).Length -gt 0 -and
    @($scenarioResult.screenshots) -contains $imageName)
Test-That 'run: install kept and recorded' ($summary.run.install -like 'kept the tested build*')
$text = Get-ChildItem -LiteralPath $runFolder.FullName -Recurse -File | Where-Object { $_.Extension -in '.json', '.md', '.log' } | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }
Test-That 'run: machine paths scrubbed from every text file' (-not (($text -join "`n").Contains($root))) 'the simulation root appears in the results'
Test-That 'run: summary.md written' (Test-Path -LiteralPath (Join-Path $runFolder.FullName 'summary.md'))

# Published to the orphan branch of the (bare) remote
$published = & git --git-dir $remote ls-tree -r --name-only verification-results 2>$null
Test-That 'publish: results/<run>/summary.json on verification-results' (@($published) -contains "results/$($runFolder.Name)/summary.json") ($published -join ' ')
Test-That 'publish: LATEST names the run' ((& git --git-dir $remote show verification-results:LATEST).Trim() -eq $runFolder.Name)
$history = @(& git --git-dir $remote log --format=%P verification-results)
Test-That 'publish: the branch is an orphan (no parent from the code history)' ($history.Count -eq 1 -and -not $history[0].Trim())
$worktreesAfter = @(& git -C $script:RepoRoot worktree list --porcelain | Where-Object { $_ -like 'worktree *' } | Sort-Object)
Test-That 'publish: temporary worktree removed and existing worktrees preserved' (
    ($worktreesBefore -join "`n") -eq ($worktreesAfter -join "`n"))

# Re-publishing finished evidence must not claim it just restored a historical install, discard unselected rows,
# or reset the original start time. Simulation would visibly rewrite "kept" to "restored" on the old implementation.
$originalStart = $summary.run.startedUtc
$originalCount = $summary.checks.Count
& $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -Only @('T-build') -Resume $runFolder.FullName -Restore -NoPush 6>&1 | Out-Null
$resumed = Get-Content -LiteralPath (Join-Path $runFolder.FullName 'summary.json') -Raw | ConvertFrom-Json
Test-That 'resume: republishing preserves all evidence and does not restore an old install' (
    $resumed.run.install -like 'kept the tested build*' -and $resumed.checks.Count -eq $originalCount -and
    $resumed.run.startedUtc -eq $originalStart -and $resumed.run.resumedUtc)

# ---- Install failure: everything that needs the install is NOT-RUN, not PASS; a second publish appends
$sim.install = @{ fails = $true }
[IO.File]::WriteAllText($simFile, ($sim | ConvertTo-Json -Depth 8))
Start-Sleep -Seconds 1
& $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -Only @('T-scenario-good', 'T-report-good') 6>&1 | Out-Null
$second = Get-ChildItem -LiteralPath (Join-Path $root 'results') -Directory | Sort-Object Name | Select-Object -Last 1
$summary = Get-Content -LiteralPath (Join-Path $second.FullName 'summary.json') -Raw | ConvertFrom-Json
$status = @{}; foreach ($c in $summary.checks) { $status[$c.id] = $c.status }
Test-That 'install failure: install FAIL' ($status['LOOP-package-install'] -eq 'FAIL') ($status | Out-String)
Test-That 'install failure: scenario NOT-RUN' ($status['T-scenario-good'] -eq 'NOT-RUN')
Test-That 'install failure: content report NOT-RUN' ($status['T-report-good'] -eq 'NOT-RUN')
$runs = @(& git --git-dir $remote ls-tree --name-only verification-results results/)
Test-That 'publish: a second run is added next to the first' ($runs.Count -eq 2) ($runs -join ' ')

# ---- Resume: finished checks are kept, NOT-RUN ones are run again
$sim.install = @{ fails = $false }
[IO.File]::WriteAllText($simFile, ($sim | ConvertTo-Json -Depth 8))
& $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -Only @('T-scenario-good', 'T-report-good') -Resume $second.FullName -NoPush 6>&1 | Out-Null
$summary = Get-Content -LiteralPath (Join-Path $second.FullName 'summary.json') -Raw | ConvertFrom-Json
$status = @{}; foreach ($c in $summary.checks) { $status[$c.id] = $c.status }
Test-That 'resume: the finished install result is kept (FAIL)' ($status['LOOP-package-install'] -eq 'FAIL') ($status | Out-String)
Test-That 'resume: checks that did not run are tried again (still NOT-RUN without an install)' ($status['T-scenario-good'] -eq 'NOT-RUN')

# ---- A game that cannot start: one scenario finds out, the rest are NOT-RUN at once (no retries for minutes each)
function Invoke-SimRun([hashtable] $World, [string[]] $Only) {
    # verify-local re-imports the game module inside its own scope, which unloads this session's copy.
    Import-Module (Join-Path $script:TestRoot 'SimulatedGame.psm1') -Force -Global
    Initialize-SimulatedGame (Join-Path $root 'game') $World @()
    Start-Sleep -Seconds 1
    & $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -Only $Only -NoPush 6>&1 | Out-Null
    $folder = Get-ChildItem -LiteralPath (Join-Path $root 'results') -Directory | Sort-Object Name | Select-Object -Last 1
    $s = Get-Content -LiteralPath (Join-Path $folder.FullName 'summary.json') -Raw | ConvertFrom-Json
    $byId = @{}; foreach ($c in $s.checks) { $byId[$c.id] = $c }
    return $byId
}
$byId = Invoke-SimRun @{ running = $false; launchFails = $true } @('T-scenario-good', 'T-scenario-after-crash')
Test-That 'launch failure: the first scenario is ERROR' ($byId['T-scenario-good'].status -eq 'ERROR') ($byId.Values | ConvertTo-Json -Compress)
Test-That 'launch failure: the next scenario is NOT-RUN with the reason' ($byId['T-scenario-after-crash'].status -eq 'NOT-RUN' -and $byId['T-scenario-after-crash'].detail -like '*could not start*') ($byId['T-scenario-after-crash'] | ConvertTo-Json -Compress)
$byId = Invoke-SimRun @{ running = $false; noAudio = $true } @('T-scenario-good', 'T-scenario-after-crash')
Test-That 'no audio device: installed, but every scenario NOT-RUN without a launch attempt' (
    $byId['LOOP-package-install'].status -eq 'PASS' -and $byId['T-scenario-good'].status -eq 'NOT-RUN' -and $byId['T-scenario-after-crash'].status -eq 'NOT-RUN' -and
    $byId['T-scenario-good'].detail -like '*audio*') ($byId.Values | ConvertTo-Json -Compress)

# ---- Development speed: quick mode passes through and is labelled; the game time cap hands the game on
function Invoke-SimRunArgs([hashtable] $World, [string[]] $Only, [hashtable] $Extra) {
    Import-Module (Join-Path $script:TestRoot 'SimulatedGame.psm1') -Force -Global
    Initialize-SimulatedGame (Join-Path $root 'game') $World @()
    Start-Sleep -Seconds 1
    & $verifyLocal -Simulate -SimulationFile $simFile -QueuePath $queuePath -Only $Only -NoPush @Extra 6>&1 | Out-Null
    $folder = Get-ChildItem -LiteralPath (Join-Path $root 'results') -Directory | Sort-Object Name | Select-Object -Last 1
    return Get-Content -LiteralPath (Join-Path $folder.FullName 'summary.json') -Raw | ConvertFrom-Json
}
$s = Invoke-SimRunArgs @{ knownCommands = @('god'); commands = @{ 'selftest' = @{ reply = 'started'; log = @('selftest_done passed=3 failed=0') } } } @('T-scenario-good') @{ Quick = $true }
Test-That 'quick run: the summary says quick (never confused with acceptance)' ([string]$s.run.mode -like '*quick*') ([string]$s.run.mode)
$s = Invoke-SimRunArgs @{ knownCommands = @('god') } @('T-scenario-good', 'T-scenario-after-crash') @{ MaxGameMinutes = 0.0001 }
# The allowance (6 ms here) may run out before the first scenario starts or while it runs: either way no scenario
# completes, each ends by the cap (killed with the cap as reason, or NOT-RUN), and the last one is NOT-RUN.
$scenarioRows = @($s.checks | Where-Object { $_.kind -eq 'scenario' })
$byCap = @($scenarioRows | Where-Object { ($_.status -eq 'NOT-RUN' -and $_.detail -like '*time cap*') -or ($_.status -eq 'ERROR' -and $_.detail -like '*remaining game time cap*') })
Test-That 'game time cap: scenarios past the cap end by the cap, the rest NOT-RUN with the reason' (
    $byCap.Count -eq 2 -and $scenarioRows[-1].status -eq 'NOT-RUN') ($s.checks | ConvertTo-Json -Compress)

# A wait for another lane is not this lane's game allowance. Mock only acquisition/process discovery, never a real game.
$phase = @{ Repo = 'mock'; Simulate = $false; Run = @{} }
$enteredAt = Get-Date
& (Get-Module VerifyLocal) {
    param($context)
    function Enter-GameLock { Start-Sleep -Milliseconds 200; return @{ Mock = $true } }
    function Get-Process { param($Name, $ErrorAction) return $null }
    Enter-GamePhase $context @()
} $phase
Test-That 'game time cap: lock waiting is excluded and recorded separately' (
    ($phase.GameSince - $enteredAt).TotalMilliseconds -ge 180 -and $phase.Run.gameLockWaitSeconds -ge 0.18)

Import-Module (Join-Path $script:RepoRoot 'tools/local/GameLock.psm1') -Force
$pluginGame = Join-Path $root 'plugin-inventory'
New-Item -ItemType Directory -Force (Join-Path $pluginGame 'plugins') | Out-Null
[IO.File]::WriteAllText((Join-Path $pluginGame 'ScriptHookDotNet.asi'), 'host')
[IO.File]::WriteAllText((Join-Path $pluginGame 'plugins/ColAccel.asi'), 'experiment')
$plugins = @(Get-GamePluginInventory $pluginGame)
Test-That 'game evidence: root and optional plugin ASIs have relative paths and actual hashes' (
    $plugins.Count -eq 2 -and @($plugins.path) -contains 'plugins/ColAccel.asi' -and
    @($plugins | Where-Object { $_.path -eq 'plugins/ColAccel.asi' })[0].sha256 -eq (Get-FileHash (Join-Path $pluginGame 'plugins/ColAccel.asi')).Hash)

$s = Invoke-SimRunArgs @{ knownCommands = @('god'); commands = @{ 'boom' = @{ reply = 'ok'; crash = $true } } } @('T-scenario-crash', 'T-scenario-after-crash') @{ StopOnFailure = $true; Restore = $true }
$first = @($s.checks | Where-Object { $_.id -eq 'T-scenario-crash' })[0]
$next = @($s.checks | Where-Object { $_.id -eq 'T-scenario-after-crash' })[0]
Test-That 'stop-on-failure: a crash skips later scenarios and still restores' (
    $first.status -eq 'CRASH' -and $next.status -eq 'NOT-RUN' -and $next.detail -like '*StopOnFailure*' -and $s.run.install -like 'restored*') ($s | ConvertTo-Json -Depth 8 -Compress)

$s = Invoke-SimRunArgs @{} @('T-verify-fails', 'T-scenario-good') @{ StopOnFailure = $true }
Test-That 'stop-on-failure: failed offline work does not install or enter the game phase' (
    @($s.checks | Where-Object { $_.id -eq 'T-verify-fails' })[0].status -eq 'FAIL' -and
    @($s.checks | Where-Object { $_.id -eq 'LOOP-package-install' })[0].status -eq 'NOT-RUN' -and $s.run.install -eq 'not installed')
# A disk/summary error after installation must still restore before relinquishing the game.
Import-Module (Join-Path $script:RepoRoot 'tools/local/VerifyLocal.psm1') -Force
$faultOptions = @{
    Repo = $script:RepoRoot; QueuePath = $queuePath; Only = @('T-scenario-good'); Kinds = @()
    ResultsRoot = (Join-Path $root 'interrupted-results'); Simulate = $true; Sim = $sim; SimRoot = $root
    Game = (Join-Path $root 'game'); GameModule = (Join-Path $script:TestRoot 'SimulatedGame.psm1')
    ScenarioDirectory = (Join-Path $root 'scenarios'); ScenarioTimeout = 120
    Restore = $true; NoPush = $true; NoManual = $true; GameInfo = @{}
}
$interrupted = & (Get-Module VerifyLocal) {
    param($options)
    function Write-Summary($Context) { $script:FaultContext = $Context; throw 'injected summary write failure' }
    try { Invoke-VerifyLocal $options | Out-Null }
    catch { $script:FaultError = $_.Exception.Message }
    return @{ install = $script:FaultContext.Run.install; error = $script:FaultError }
} $faultOptions
Test-That 'unexpected exception: restoration runs and the original error survives' (
    $interrupted.install -like 'restored*' -and $interrupted.error -eq 'injected summary write failure') ($interrupted | ConvertTo-Json -Compress)
$env:LIBERTY_SIM_STATE = $null
