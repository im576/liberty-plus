# T-042: tools/perf/GunplayRange.psm1 reads the range_shot lines of a range run.
Import-Module (Join-Path $script:RepoRoot 'tools/perf/GunplayRange.psm1') -Force

$log = @(
    '2026-09-30T10:00:00.000Z [INFO] range_started aim=(1,2,3) weapon=7 profile=glock_stage1',
    '2026-09-30T10:00:01.000Z [INFO] range_shot weapon=7 n=1 gap_ms=-1 dev=0.120 cone=0.300 range_m=25.0 state=standing-hip speed=0.0',
    '2026-09-30T10:00:03.000Z [INFO] range_shot weapon=7 n=1 gap_ms=2000 dev=0.400 cone=0.300 range_m=25.0 state=standing speed=0.0',
    '2026-09-30T10:00:05.000Z [INFO] range_shot weapon=7 n=1 gap_ms=2000 dev=0.250 cone=0.300 range_m=25.0 state=standing speed=0.0',
    '2026-09-30T10:00:05.300Z [INFO] range_shot weapon=7 n=2 gap_ms=300 dev=0.500 cone=0.450 range_m=25.0 state=standing speed=0.0',
    '2026-09-30T10:00:05.600Z [INFO] range_shot weapon=7 n=3 gap_ms=300 dev=1.500 cone=0.600 range_m=25.0 state=standing speed=0.0',
    '2026-09-30T10:00:05.900Z [INFO] range_shot weapon=7 n=4 gap_ms=300 dev=0.700 cone=0.750 range_m=25.0 state=standing speed=0.0',
    '2026-09-30T10:00:06.000Z [INFO] range_shot weapon=14 n=1 gap_ms=-1 dev=0.100 cone=0.350 range_m=24.5 state=standing speed=0.0',
    '2026-09-30T10:00:06.100Z [INFO] some other line dev=9 cone=9'
)
$shots = ConvertFrom-RangeLog $log
Test-That 'range log: only range_shot lines are read' ($shots.Count -eq 7 -and $shots[0].weapon -eq 7 -and $shots[6].weapon -eq 14 -and $shots[0].gapMs -eq -1)
Test-That 'range log: values are parsed with the invariant culture' ($shots[1].deviation -eq 0.4 -and $shots[1].cone -eq 0.3 -and $shots[6].rangeMeters -eq 24.5)
Test-That 'range groups: first, burst (2-3), sustained (4+)' ((Get-RangeGroup 1) -eq 'first' -and (Get-RangeGroup 2) -eq 'burst' -and (Get-RangeGroup 3) -eq 'burst' -and (Get-RangeGroup 4) -eq 'sustained')
$summary = Get-RangeSummary $shots
$first = @($summary | Where-Object { $_.weapon -eq 7 -and $_.group -eq 'first' })[0]
$burst = @($summary | Where-Object { $_.weapon -eq 7 -and $_.group -eq 'burst' })[0]
$rifleBurst = @($summary | Where-Object { $_.weapon -eq 14 -and $_.group -eq 'burst' })[0]
Test-That 'range summary: first shots count, mean, p95, max' ($first.n -eq 3 -and $first.meanDeviation -eq 0.257 -and $first.p95Deviation -eq 0.4 -and $first.maxDeviation -eq 0.4)
Test-That 'range summary: inside the cone uses the 5% + 0.02 degree tolerance' ($first.insideCone -eq 0.67 -and $burst.n -eq 2 -and $burst.insideCone -eq 0)
Test-That 'range summary: a group with no shots is reported empty, not dropped' ($rifleBurst.n -eq 0)
$table = Format-RangeMarkdown $summary @{ 7 = 'Glock 17' }
Test-That 'range markdown: a header, one row per weapon and group, labels applied' ($table.Count -eq 2 + 6 -and ($table -join "`n") -match 'Glock 17 \(7\)')
