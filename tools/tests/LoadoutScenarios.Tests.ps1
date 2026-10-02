# T-044 scenarios: the generated outfit and weapon-class reviews match config/holsters.json, and every hand-written loadout
# scenario only uses commands the game-side autopilot registers.
$generator = Join-Path $script:RepoRoot 'tools/perf/New-LoadoutScenarios.ps1'
$check = & (Get-Process -Id $PID).Path -NoProfile -File $generator -Check 2>&1 | Out-String
Test-That 'loadout scenarios: the generated outfit and review scenarios are up to date with config/holsters.json' ($LASTEXITCODE -eq 0) $check

$holsters = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'config/holsters.json') -Raw | ConvertFrom-Json
$outfits = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'tools/autopilot/scenarios/stage1-loadout-outfits.txt') -Raw
$bulky = @($holsters.outfitClasses | Where-Object { $_.id -eq 'bulky' })[0]
Test-That 'loadout scenarios: every upper-body drawable has its own front, side and back screenshot' (@(0..16 | Where-Object {
    $tag = '{0:D2}' -f $_
    $outfits -match "(?m)^shot outfit_${tag}_front\r?$" -and $outfits -match "(?m)^shot outfit_${tag}_side\r?$" -and $outfits -match "(?m)^shot outfit_${tag}_back\r?$" }).Count -eq 17)
Test-That 'loadout scenarios: each bulky drawable is expected to classify as bulky' (@($bulky.drawables | Where-Object { $outfits -match "(?m)^holsters outfit 1 $_ 0\r?\n(?:.*\r?\n){3}expect ""holsters_status [^""]*outfit=bulky " }).Count -eq @($bulky.drawables).Count)

# Every command word in the loadout scenarios is registered by the autopilot mod, the engine or the modules' own commands.
$known = @('await-alive', 'events', 'god', 'wanted', 'goto', 'time', 'weather', 'strip', 'give', 'buy', 'select', 'spawncar', 'enter', 'leave', 'clear',
    'cycle-vehicle', 'cycle-weapons', 'cycle-deaths', 'holsters', 'arsenal', 'hud', 'cam', 'label', 'framestats', 'costs', 'perf', 'pools', 'modules',
    'wait', 'shot', 'expect', 'expectmarked', 'mark', 'key')
$unknown = @()
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $script:RepoRoot 'tools/autopilot/scenarios') -Filter 'stage1-loadout-*.txt') {
    foreach ($line in Get-Content -LiteralPath $file.FullName) {
        $text = $line.Trim()
        if ($text.Length -eq 0 -or $text.StartsWith('#')) { continue }
        $word = ($text -split '\s+')[0]
        if ($known -notcontains $word) { $unknown += "$($file.Name): $word" }
    }
}
Test-That 'loadout scenarios: only known commands are used' ($unknown.Count -eq 0) ($unknown -join '; ')
$autopilot = Get-Content -LiteralPath (Join-Path $script:RepoRoot 'mods/Liberty.Autopilot/AutopilotModule.cs') -Raw
$missing = @('strip', 'buy', 'enter', 'leave', 'cycle-vehicle', 'cycle-weapons', 'cycle-deaths') | Where-Object { $autopilot -notmatch "Register\(""$_""" }
Test-That 'loadout scenarios: the autopilot mod registers the commands they use' (@($missing).Count -eq 0) (@($missing) -join ', ')
