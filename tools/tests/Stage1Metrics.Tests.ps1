# T-040: the Stage 1 measurement tooling (tools/perf): reply parsing, sections, summaries, comparison, and that the generated
# scenarios match the capture points in config/devtools/locations.json.
Import-Module (Join-Path $script:RepoRoot 'tools/perf/Stage1Metrics.psm1') -Force

# ---- reply parsing
$perf = ConvertFrom-KeyValueReply 'frame_ms=16.70 p95_ms=18.10 pressure=0.00 private_mb=1650 working_set_mb=1500 address_free_mb=900 largest_free_block_mb=512 managed_mb=40 physical_load=63% core_us=210.5'
Test-That 'perf reply: values by key' ($perf['frame_ms'] -eq '16.70' -and $perf['private_mb'] -eq '1650' -and $perf['physical_load'] -eq '63%')
$costs = ConvertFrom-CostsReply 'costs_ms(avg/max/count@thread) engine.frame=2.500/9.1/600@1 total=1500 gunplay.tick=0.900/3.2/600@1 total=540 combat.tick=0.050/1.0/20@1 total=1'
Test-That 'costs reply: sections with avg/max/count/total' ($costs.Count -eq 3 -and $costs['engine.frame'].avgMs -eq 2.5 -and $costs['engine.frame'].count -eq 600 -and $costs['gunplay.tick'].maxMs -eq 3.2)
Test-That 'costs reply: nothing recorded is an empty table' ((ConvertFrom-CostsReply 'costs_ms(avg/max/count@thread)').Count -eq 0)
$merged = Merge-CostWindows @($costs, (ConvertFrom-CostsReply 'costs_ms(avg/max/count@thread) engine.frame=3.500/12.0/300@1 total=1050'))
Test-That 'cost windows: call-weighted mean, largest max, summed count' ($merged['engine.frame'].count -eq 900 -and [math]::Abs($merged['engine.frame'].avgMs - 2.8333) -lt 0.001 -and $merged['engine.frame'].maxMs -eq 12.0)

# ---- sections from a command log
$log = @(
    '2026-09-30T10:00:00.000Z [INFO] command source=file:c1 line="modules" reply="gunplay avg=0.1"',
    '2026-09-30T10:00:01.000Z [INFO] command source=file:c2 line="label s1_test_day" reply="label s1_test_day"',
    '2026-09-30T10:00:01.100Z [INFO] command source=file:c3 line="framestats" reply="frames=99 avg_ms=1.00 p50_ms=1.0 p95_ms=1.0 p99_ms=1.0 max_ms=1.0 over33_ms=0 over100_ms=0 stalls_1s=0"',
    '2026-09-30T10:00:01.200Z [INFO] command source=file:c4 line="costs" reply="costs_ms(avg/max/count@thread) engine.frame=9.000/9.0/1@1 total=9"',
    '2026-09-30T10:00:09.000Z [INFO] command source=file:c5 line="perf" reply="frame_ms=16.70 p95_ms=18.10 pressure=0.00 private_mb=1650 address_free_mb=900 largest_free_block_mb=512 managed_mb=40"',
    '2026-09-30T10:00:09.100Z [INFO] command source=file:c6 line="framestats" reply="frames=480 avg_ms=16.80 p50_ms=16.6 p95_ms=19.5 p99_ms=24.0 max_ms=41.2 over33_ms=2 over100_ms=1 stalls_1s=0"',
    '2026-09-30T10:00:09.200Z [INFO] command source=file:c7 line="costs" reply="costs_ms(avg/max/count@thread) engine.frame=2.500/9.1/480@1 total=1200"',
    '2026-09-30T10:00:09.300Z [INFO] command source=file:c8 line="pools" reply="peds=30/100 vehicles=20/60 objects=5/200"',
    '2026-09-30T10:00:10.000Z [INFO] command source=file:c9 line="label s1_test_night" reply="label s1_test_night"',
    '2026-09-30T10:00:10.100Z [INFO] command source=file:c10 line="framestats" reply="frames=0"',
    '2026-09-30T10:00:20.000Z [INFO] command source=file:c11 line="framestats" reply="frames=0"'
)
$records = Get-CommandRecords $log
Test-That 'command records: every command line in order' ($records.Count -eq 11 -and $records[1].Command -eq 'label s1_test_day' -and $records[4].Reply -match '^frame_ms=')
$gpu = @([pscustomobject]@{ label = 's1_test_day'; gpuDedicatedMB = 1500.0; privateMB = 1800.0; workingSetMB = 1700.0; systemCpuPercent = 20.0; gameDiskBusyPercent = 8.0 }, [pscustomobject]@{ label = 's1_test_day_extra'; gpuDedicatedMB = 1600.0; privateMB = 1900.0; workingSetMB = 1750.0; systemCpuPercent = 45.0; gameDiskBusyPercent = 97.0 }, [pscustomobject]@{ label = 'other'; gpuDedicatedMB = 9999.0; privateMB = 1; workingSetMB = 1 })
$sections = @(Get-MeasuredSections $records $gpu)
Test-That 'sections: one per label' ($sections.Count -eq 2 -and $sections[0].label -eq 's1_test_day' -and $sections[1].label -eq 's1_test_night')
$day = $sections[0]
Test-That 'sections: the frame statistics are the last framestats (the first only reset)' ($day.frames -eq 480 -and $day.p99_ms -eq 24.0 -and $day.p50_ms -eq 16.6 -and $day.over100 -eq 1 -and $day.stalls1s -eq 0)
Test-That 'sections: the first costs after the label only reset the meter' ($day.costs['engine.frame'].avgMs -eq 2.5 -and $day.costs['engine.frame'].count -eq 480)
Test-That 'sections: perf and pools of the section' ($day.perf.privateMbMax -eq 1650 -and $day.perf.addressFreeMbMin -eq 900 -and $day.pools['peds'] -eq '30/100')
Test-That 'sections: GPU measurements with the label or "<label>_" only' ($day.gpuSamples -eq 2 -and $day.gpuDedicatedMbMax -eq 1600.0 -and $day.privateMbMax -eq 1900.0)
Test-That 'sections: the highest PC load during the section is kept (a busy disk explains hitches)' ($day.systemCpuPercentMax -eq 45.0 -and $day.gameDiskBusyPercentMax -eq 97.0 -and -not $day.Contains('gameDiskQueueMax'))
Test-That 'sections: a label with no frames has no frame statistics, not zeros' ($null -eq $sections[1].frames -and $null -eq $sections[1].p95_ms)

# ---- input from outside the scenario (someone playing during the run)
$noisy = $log + @(
    '2026-09-30T10:00:05.000Z [INFO] weapon_changed from=12 to=7 profile=vanilla',
    '2026-09-30T10:00:06.000Z [INFO] combat_hit region=LeftArm bone=0x4C1 damage=200 weapon=16 dead=False',
    '2026-09-30T10:00:10.500Z [INFO] arsenal_storage_open id=temporary:00003508')
$noisyRecords = Get-CommandRecords $noisy
$found = @(Get-InterferenceLines $noisy $noisyRecords)
Test-That 'interference: weapon switches, hits and trunk use in a scenario that issues no combat commands' ($found.Count -eq 3)
$noisySections = @(Get-MeasuredSections $noisyRecords @() $found)
Test-That 'interference: counted in the section whose window holds the line (label to next label)' ($noisySections[0].interference -eq 2 -and $noisySections[1].interference -eq 1)
$armed = $noisy + '2026-09-30T10:00:04.000Z [INFO] command source=file:c99 line="give 14 300" reply="gave 14"'
$armedFound = @(Get-InterferenceLines $armed (Get-CommandRecords $armed))
Test-That 'interference: a scenario that arms or spawns subjects causes weapon and hit lines itself; only trunk and menu use still counts' ($armedFound.Count -eq 1 -and $armedFound[0] -match 'arsenal_storage_open')
Test-That 'interference: the clean log has none' ((Get-InterferenceLines $log $records).Count -eq 0)

# ---- a report folder
$report = Join-Path $script:Scratch 'stage1-report-on'
New-Item -ItemType Directory -Force -Path $report | Out-Null
[IO.File]::WriteAllText((Join-Path $report 'report.md'), "# Scenario stage1-capture-test`n`n- Result: PASS`n")
[IO.File]::WriteAllLines((Join-Path $report 'run.log'), ($log + '2026-09-30T10:00:11.000Z [WARN] engine_stall ms=6000'))
[IO.File]::WriteAllText((Join-Path $report 'measurements.json'), '[ { "label": "s1_test_day", "gpuDedicatedMB": 1500.0, "privateMB": 1800.0, "workingSetMB": 1700.0 }, { "label": "s1_test_night", "gpuDedicatedMB": 1520.0, "privateMB": 1810.0, "workingSetMB": 1710.0 } ]')
$on = Get-ReportSummary $report
Test-That 'report summary: several GPU measurements are read as separate samples (PowerShell 5.1 JSON arrays)' (@($on.sections | Where-Object { $_.label -eq 's1_test_day' })[0].gpuDedicatedMbMax -eq 1500.0)
Test-That 'report summary: scenario, condition, stalls and sections' ($on.scenario -eq 'stage1-capture-test' -and $on.condition -eq 'mod-on' -and $on.engineStalls -eq 1 -and @($on.sections).Count -eq 2)
$offReport = Join-Path $script:Scratch 'stage1-report-off'
New-Item -ItemType Directory -Force -Path $offReport | Out-Null
[IO.File]::WriteAllText((Join-Path $offReport 'report.md'), "# Scenario stage1-capture-test-off`n")
$offLog = @('2026-09-30T09:00:00.000Z [INFO] command source=file:c0 line="stop gunplay" reply="stopped gunplay"') + $log
$offLog = $offLog | ForEach-Object { $_ -replace 'p95_ms=19.5 p99_ms=24.0', 'p95_ms=17.0 p99_ms=20.0' -replace 'engine.frame=2.500', 'engine.frame=0.300' }
[IO.File]::WriteAllLines((Join-Path $offReport 'run.log'), $offLog)
[IO.File]::WriteAllText((Join-Path $offReport 'measurements.json'), '[ { "label": "s1_test_day", "gpuDedicatedMB": 1200.0, "privateMB": 1700.0, "workingSetMB": 1600.0 } ]')
$off = Get-ReportSummary $offReport
Test-That 'report summary: stopping the gameplay modules is mod-off' ($off.condition -eq 'mod-off')
Test-That 'report summary: an explicit condition wins over the log (engine.json disabledModules runs)' ((Get-ReportSummary $report 'mod-off').condition -eq 'mod-off')
Test-That 'report summary: no density lines means no density figure, not 1.0' ($null -eq $on.densityMinPeds -and $on.densityLines -eq 0)
$densityReport = Join-Path $script:Scratch 'stage1-report-density'
New-Item -ItemType Directory -Force -Path $densityReport | Out-Null
[IO.File]::WriteAllLines((Join-Path $densityReport 'run.log'), @(
    '2026-09-30T09:59:00.000Z [INFO] density frame_ms=50.0 peds=0.40 cars=0.40',
    '2026-09-30T10:00:00.000Z [INFO] command source=file:c1 line="label s1_x_day" reply="label s1_x_day"',
    '2026-09-30T10:00:10.000Z [INFO] density frame_ms=22.0 peds=1.00 cars=1.00',
    '2026-09-30T10:00:30.000Z [INFO] density frame_ms=38.0 peds=0.72 cars=0.80',
    '2026-09-30T10:01:00.000Z [INFO] density frame_ms=30.0 peds=0.90 cars=0.85',
    '2026-09-30T10:01:10.000Z [INFO] weapon_changed from=-1 to=12 profile=vanilla'))
$density = Get-ReportSummary $densityReport
Test-That 'report summary: the lowest density the governor reached while measuring is reported (lines before the first label do not count)' ($density.densityMinPeds -eq 0.72 -and $density.densityMinCars -eq 0.8 -and $density.densityLines -eq 3)
Test-That 'report summary: the first weapon of a restarted gunplay module is not outside input' ($density.interferenceLines -eq 0)

# ---- the script end to end: summarise, then compare (through JSON, as the owner uses it)
$measure = Join-Path $script:RepoRoot 'tools/perf/Measure-Stage1.ps1'
$summaryOut = Join-Path $script:Scratch 'stage1-out/summary.json'
& $measure -Reports $report, $offReport -Out $summaryOut 6>$null
$onJson = Join-Path $script:Scratch 'stage1-out/summary.mod-on.json'
$offJson = Join-Path $script:Scratch 'stage1-out/summary.mod-off.json'
Test-That 'Measure-Stage1: one JSON per condition' ((Test-Path -LiteralPath $onJson) -and (Test-Path -LiteralPath $offJson))
$markdown = Join-Path $script:Scratch 'stage1-out/compare.md'
& $measure -On $onJson -Off $offJson -Markdown $markdown 6>$null
$table = Get-Content -LiteralPath $markdown -Raw
Test-That 'comparison: mod-on against mod-off, both values and the change' ($table -match 's1_test_day' -and $table -match '19\.5 / 17\.0' -and $table -match '\+14\.7%')
Test-That 'comparison: p95 above +10% is marked OVER, p99 within +15% is not' ($table -match '19\.5 / 17\.0 \(\+14\.7% OVER\)' -and $table -match '24\.0 / 20\.0 \(\+20\.0% OVER\)')
Test-That 'comparison: GPU memory change against the 350 MB ceiling (+300 MB is within)' ($table -match '1500 / 1200 \(\+300\.0 MB\)')
Test-That 'comparison: engine.frame cost on / off' ($table -match '2\.500 / 0\.300')

# ---- the generated scenarios match config/devtools/locations.json
$generator = Join-Path $script:RepoRoot 'tools/perf/New-Stage1Scenarios.ps1'
$check = & (Get-Process -Id $PID).Path -NoProfile -File $generator -Check 2>&1 | Out-String
Test-That 'scenarios: the Stage 1 scenario files are up to date with the capture points' ($LASTEXITCODE -eq 0) $check
$locations = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'config/devtools/locations.json') -Raw | ConvertFrom-Json
$points = @($locations.locations | Where-Object { $_.id -like 's1_*' })
Test-That 'capture points: 12 s1_* points, all snap none, with a heading' ($points.Count -eq 12 -and @($points | Where-Object { $_.snap -ne 'none' }).Count -eq 0 -and @($points | Where-Object { $null -eq $_.heading }).Count -eq 0)
Test-That 'capture points: ids are unique' (@($locations.locations | ForEach-Object { $_.id } | Sort-Object -Unique).Count -eq @($locations.locations).Count)
$capture = (Get-Content -LiteralPath (Join-Path $script:RepoRoot 'tools/autopilot/scenarios/stage1-capture-broker.txt') -Raw) + (Get-Content -LiteralPath (Join-Path $script:RepoRoot 'tools/autopilot/scenarios/stage1-capture-city.txt') -Raw)
Test-That 'scenarios: every capture point is visited in day and night with a screenshot each' (@($points | Where-Object { $capture -match "(?m)^goto $($_.id)\r?$" -and $capture -match "(?m)^shot $($_.id)_day\r?$" -and $capture -match "(?m)^shot $($_.id)_night\r?$" }).Count -eq 12)
