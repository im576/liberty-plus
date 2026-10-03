param(
    [Parameter(Mandatory = $true)][string] $GameDirectory,
    [Parameter(Mandatory = $true)][string] $ScriptHookDotNetReference,
    # Extracted Liberty Vehicle Services CE release (MIT, ekzestean): source for the T-023 label patch.
    [string] $LvsDirectory,
    # Skip the validation steps that tools/verify-local.ps1 already runs as its own checks (offline verifier, model and
    # content self-tests). The artifacts are built the same way; only the repeated validation is left out.
    [switch] $Fast,
    # Build: compile and generate every artifact into staging\phase2-build. It reads only the repository and the game's own
    #        archives, never what an install writes, so it may run while another session uses the game.
    # Stage: write staging\phase2 (files + manifest) from those artifacts and the installed files it merges with (arsenal
    #        and holster settings, locations, WeaponInfo.xml, default.dat). Seconds; verify-local runs it under the game lock.
    # All (default): both, as before.
    [ValidateSet('All', 'Build', 'Stage')][string] $Phase = 'All'
)

# Package only Phase 2 scripts/config. Phase 1's already-installed gold models and
# WeaponInfo.xml remain the base; the old Phase 1 asset builder expects vanilla inputs.
# Unchanged steps are skipped (tools/BuildCache.psm1): the C#, core and content-compiler builds keep stamps in their bin
# folders, and the game-derived artifacts (vehicle extras, weapon icons, sling models, content models) are reused from the
# machine's build cache when their inputs and the game's archives are the same. LIBERTY_NO_BUILD_CACHE=1 rebuilds all.
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath (Split-Path -Parent $PSScriptRoot)).Path
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
Import-Module (Join-Path $PSScriptRoot 'PackageMerge.psm1') -Force 3>$null
Import-Module (Join-Path $PSScriptRoot 'BuildCache.psm1') -Force
$stage = [IO.Path]::GetFullPath((Join-Path $repoRoot 'staging\phase2'))
$built = [IO.Path]::GetFullPath((Join-Path $repoRoot 'staging\phase2-build'))
foreach ($folder in $stage, $built) {
    if (-not $folder.StartsWith($repoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging path.' }
}
$buildInfoPath = Join-Path $built 'build.json'

# git reports on stderr; with 'Stop', Windows PowerShell 5.1 would turn that into a terminating error.
function Get-RepoState {
    if (Test-Path -LiteralPath (Join-Path $repoRoot 'workspace.json')) {
        $workspace = Get-Content -LiteralPath (Join-Path $repoRoot 'workspace.json') -Raw | ConvertFrom-Json
        $snapshotCommit = (& git -C $repoRoot rev-parse HEAD 2>$null | Out-String).Trim()
        return [ordered]@{ commit = $snapshotCommit;
            dirty = [bool]($workspace.repositories.framework.dirty -or $workspace.repositories.mod.dirty);
            repositories = $workspace.repositories }
    }
    $ErrorActionPreference = 'Continue'
    $commit = (& git -C $repoRoot rev-parse HEAD 2>$null | Out-String).Trim()
    $dirty = [bool]((& git -C $repoRoot status --porcelain --untracked-files=no 2>$null | Out-String).Trim())
    return [ordered]@{ commit = $commit; dirty = $dirty; repositories = $null }
}

function Get-RepoFiles([string] $relativeFolder, [string] $filter, [switch] $SkipProgram, [switch] $Recurse) {
    $folder = Join-Path $repoRoot $relativeFolder
    if (-not (Test-Path -LiteralPath $folder)) { return @() }
    $items = if ($Recurse) { Get-ChildItem -LiteralPath $folder -Recurse -File -Filter $filter } else { Get-ChildItem -LiteralPath $folder -File -Filter $filter }
    return @($items | Where-Object { -not ($SkipProgram -and $_.Name -eq 'Program.cs') } | ForEach-Object { $_.FullName })
}

# =====================================================================================================================
# Build
if ($Phase -ne 'Stage') {
    # A new build invalidates the staged package (and keeps the offline verifier from reading a stale one).
    # $stage and $built are checked to be inside the repository above.
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $built | Out-Null
    Remove-Item -LiteralPath $buildInfoPath -Force -ErrorAction SilentlyContinue
    $state = Get-RepoState

    & (Join-Path $PSScriptRoot 'build.ps1') -ScriptHookDotNetReference $ScriptHookDotNetReference
    if ($LASTEXITCODE -ne 0) { throw 'Phase 2 build failed.' }
    & (Join-Path $PSScriptRoot 'build-core.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'LibertyCore build failed.' }
    if (-not $Fast) {
        & (Join-Path $PSScriptRoot 'verify.ps1') -GameDirectory $game | Select-String -Pattern '^(FAIL|RESULT)'
        if ($LASTEXITCODE -ne 0) { throw 'Phase 2 offline verification failed.' }
    }

    $gameKey = Get-GameFingerprint $game
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
    $toolFlags = @('/nologo', '/target:exe', '/platform:x86', '/warn:4', '/warnaserror+', '/reference:System.Runtime.Serialization.dll', '/reference:System.Drawing.dll', '/reference:System.Core.dll')
    $toolWork = Join-Path $built '_tools'
    New-Item -ItemType Directory -Force -Path $toolWork | Out-Null
    function Build-Tool([string] $exe, [string[]] $sources) {
        & $compiler @toolFlags "/out:$exe" $sources
        if ($LASTEXITCODE -ne 0) { throw "$(Split-Path -Leaf $exe) build failed." }
    }
    $finishSources = Get-RepoFiles 'tools\finishes' '*.cs' -SkipProgram
    $toolExtra = @("csc|$(Get-FileIdentity $compiler)", "flags|$($toolFlags -join ' ')", "game|$gameKey")

    # T-023: body-part catalog of vehicle extras, generated from the player's own vehicles.img (read-only),
    # plus LVS CE with its six workshop "Extra N" labels routed through that catalog.
    $scannerSources = $finishSources + (Get-RepoFiles 'tools\vehicles' '*.cs')
    $extrasOut = Join-Path $built 'extras'
    Invoke-CachedStep 'vehicle-extras' (Get-InputKey $repoRoot $scannerSources $toolExtra) $extrasOut {
        $scanner = Join-Path $toolWork 'VehicleExtras.exe'
        Build-Tool $scanner $scannerSources
        & $scanner $game (Join-Path $extrasOut 'vehicle_extras.json')
        if ($LASTEXITCODE -ne 0) { throw 'Vehicle extras scan failed.' }
    } | Out-Null

    # S-2: weapon wheel icons = each installed weapon model's own HUD icon texture, extracted from the player's files.
    $iconSources = $finishSources + (Get-RepoFiles 'tools\ui' '*.cs')
    $iconsOut = Join-Path $built 'icons'
    Invoke-CachedStep 'weapon-icons' (Get-InputKey $repoRoot $iconSources $toolExtra) $iconsOut {
        $iconTool = Join-Path $toolWork 'WeaponIcons.exe'
        Build-Tool $iconTool $iconSources
        & $iconTool $game $iconsOut
        if ($LASTEXITCODE -ne 0) { throw 'Weapon icon extraction failed.' }
    } | Out-Null

    # W-5 / T-2: body-fitted sling straps built by tools/models from the player's own playerped.rpf and a weapons.img
    # prop template (config/models/sling.json). Registered through lf_models.ide, added to default.dat once.
    $modelSources = $finishSources + (Get-RepoFiles 'tools\models' '*.cs')
    $modelTool = Join-Path $toolWork 'LibertyModel.exe'
    if (-not $Fast) {
        Build-Tool $modelTool $modelSources
        & $modelTool selftest $game 'pc\models\cdimages\weapons.img' | Select-Object -Last 1
        if ($LASTEXITCODE -ne 0) { throw 'Model tool round-trip self-test failed.' }
    }
    $slingConfig = Join-Path $repoRoot 'config\models\sling.json'
    $modelsOut = Join-Path $built 'models'
    Invoke-CachedStep 'sling-models' (Get-InputKey $repoRoot ($modelSources + @($slingConfig)) $toolExtra) $modelsOut {
        if (-not (Test-Path -LiteralPath $modelTool)) { Build-Tool $modelTool $modelSources }
        & $modelTool sling $game $slingConfig $modelsOut
        if ($LASTEXITCODE -ne 0) { throw 'Sling build failed.' }
    } | Out-Null

    # M4: Liberty Content Compiler. Every content/**/asset.json (Blender/glTF sources) is validated, compiled, read back and
    # packed into LibertyContent.img + lf_content.ide; a failed asset fails the package. The compiler itself is always
    # brought up to date (other checks run it); the compiled models are reused while content, compiler and game are unchanged.
    & (Join-Path $PSScriptRoot 'build-content.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'Content compiler build failed.' }
    $contentExe = Join-Path $repoRoot 'tools\content\bin\LibertyContent.exe'
    if (-not $Fast) {
        & $contentExe selftest
        if ($LASTEXITCODE -ne 0) { throw 'Content compiler self-test failed.' }
    }
    $contentOut = Join-Path $built 'content'
    $contentAssets = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'content') -Recurse -Filter 'asset.json' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName } | Sort-Object)
    $reports = Join-Path $repoRoot 'staging\phase2-reports'
    if (Test-Path -LiteralPath $reports) { Remove-Item -LiteralPath $reports -Recurse -Force }
    if ($contentAssets.Count -gt 0) {
        $contentInputs = (Get-RepoFiles 'content' '*' -Recurse) + (Get-RepoFiles 'tools\content' '*.cs') + (Get-RepoFiles 'tools\models' '*.cs' -SkipProgram) + $finishSources +
            @((Join-Path $PSScriptRoot 'build-content.ps1'))
        $contentKey = Get-InputKey $repoRoot $contentInputs @("game|$gameKey", "assets|$(($contentAssets | ForEach-Object { $_.Substring($repoRoot.Length) }) -join ',')")
        Invoke-CachedStep 'content' $contentKey $contentOut {
            & $contentExe package $game $contentOut 'LibertyContent.img' 'lf_content.ide' @contentAssets
            if ($LASTEXITCODE -ne 0) { throw 'Content build failed (see the asset reports).' }
        } | Out-Null
        # Keep the content compiler's reports and previews (JSON and PNG of this repository's own assets; never the compiled
        # .wdr/.wtd, which are built on game templates) for review and for tools/verify-local.ps1.
        foreach ($report in Get-ChildItem -LiteralPath $contentOut -Recurse -File | Where-Object { $_.Extension -in '.json', '.png' }) {
            $relative = $report.FullName.Substring($contentOut.Length).TrimStart('\', '/')
            $target = Join-Path (Join-Path $reports 'content') $relative
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
            Copy-Item -LiteralPath $report.FullName -Destination $target
        }
    }
    elseif (Test-Path -LiteralPath $contentOut) { Remove-Item -LiteralPath $contentOut -Recurse -Force }

    $lvsOut = Join-Path $built 'lvs'
    if (Test-Path -LiteralPath $lvsOut) { Remove-Item -LiteralPath $lvsOut -Recurse -Force }
    if ($LvsDirectory) {
        New-Item -ItemType Directory -Force -Path $lvsOut | Out-Null
        $lvsSource = Join-Path (Resolve-Path -LiteralPath $LvsDirectory).Path 'scripts\LibertyVehicleServicesCE.CS'
        $lvsPatched = Join-Path $lvsOut 'LibertyVehicleServicesCE.CS'
        Copy-Item -LiteralPath $lvsSource -Destination $lvsPatched -Force
        & (Join-Path $repoRoot 'tools\vehicles\patch-lvs-labels.ps1') -LvsScript $lvsPatched
        # SHDN compiles .cs scripts at load; prove the patched script compiles against the same runtime first.
        & $compiler /nologo /target:library /platform:x86 "/out:$(Join-Path $toolWork 'lvs_check.dll')" "/reference:$ScriptHookDotNetReference" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $lvsPatched | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Patched LibertyVehicleServicesCE.CS does not compile.' }
    }
    Remove-Item -LiteralPath $toolWork -Recurse -Force -ErrorAction SilentlyContinue

    # Snapshot every repository file the stage step reads (built DLLs, config), so staging later - after a wait for the
    # game lock - installs exactly this build even if the worktree is edited or rebuilt meanwhile.
    $snapshot = Join-Path $built 'files'
    if (Test-Path -LiteralPath $snapshot) { Remove-Item -LiteralPath $snapshot -Recurse -Force }
    $stageInputs = @('src\LibertyFramework\bin\Release\LibertyFramework.net.dll', 'native\LibertyCore\bin\LibertyCore.dll', 'sdk\Liberty.Sdk\bin\Liberty.Sdk.dll',
        'config\gunplay.json', 'config\combat_effects.json', 'config\weapon-catalog.json', 'config\atmosphere.json', 'config\engine.json',
        'config\arsenal.json', 'config\holsters.json', 'config\devtools\locations.json', 'config\world\objects.json')
    $stageInputs += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'mods') -Directory | ForEach-Object { "mods\$($_.Name)\bin\$($_.Name).dll" } |
        Where-Object { Test-Path -LiteralPath (Join-Path $repoRoot $_) })
    $stageInputs += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'config\presets') -Filter '*.json' | ForEach-Object { "config\presets\$($_.Name)" })
    foreach ($relative in $stageInputs) {
        $source = Join-Path $repoRoot $relative
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing Phase 2 source: $source" }
        $target = Join-Path $snapshot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }

    $info = [ordered]@{ repo = $repoRoot; commit = $state.commit; dirty = $state.dirty; repositories = $state.repositories; builtUtc = [DateTime]::UtcNow.ToString('o'); lvs = [bool]$LvsDirectory }
    [IO.File]::WriteAllText($buildInfoPath, ($info | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
    Write-Host "Phase 2 artifacts built in $built"
}

if ($Phase -eq 'Build') { exit 0 }

# =====================================================================================================================
# Stage
# Everything below reads the build's snapshot (staging\phase2-build), never the worktree, plus the installed files it merges.
if (-not (Test-Path -LiteralPath $buildInfoPath)) { throw "No build in $built; run package-phase2.ps1 -Phase Build first." }
$info = Get-Content -LiteralPath $buildInfoPath -Raw | ConvertFrom-Json
$snapshot = Join-Path $built 'files'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null

function Stage-File([string] $source, [string] $relativePath, [string] $policy) {
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing Phase 2 source: $source" }
    $target = Join-Path $stage $relativePath
    $installed = Join-Path $game $relativePath
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
    if ($policy -eq 'merge-defaults' -and (Test-Path -LiteralPath $installed -PathType Leaf)) {
        $template = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
        $saved = Get-Content -LiteralPath $installed -Raw | ConvertFrom-Json
        if ($saved.schemaVersion -ne $template.schemaVersion) { throw "Cannot merge schema for $relativePath" }
        foreach ($property in $template.PSObject.Properties) {
            if (-not $saved.PSObject.Properties[$property.Name]) {
                $saved | Add-Member -NotePropertyName $property.Name -NotePropertyValue $property.Value
            }
        }
        [IO.File]::WriteAllText($target, ($saved | ConvertTo-Json -Depth 32), (New-Object Text.UTF8Encoding($false)))
    }
    elseif ($policy -eq 'patch-weaponinfo') {
        # T-041: the installed WeaponInfo.xml (the weapon pack's, plus the LF_GOLD_* entries) with the catalog's identity stats.
        if (-not (Test-Path -LiteralPath $installed -PathType Leaf)) { throw "Missing installed $relativePath (install the weapon pack first)." }
        $patched = Merge-WeaponInfoStats (Get-Content -LiteralPath $source -Raw) ([IO.File]::ReadAllText($installed))
        [IO.File]::WriteAllText($target, $patched.Xml, (New-Object Text.UTF8Encoding($false)))
        if ($patched.Changes.Count -gt 0) { Write-Host "weaponinfo: $($patched.Changes.Count) stat changes: $($patched.Changes -join '; ')" } else { Write-Host 'weaponinfo: installed stats already match the catalog' }
    }
    elseif ($policy -eq 'merge-locations' -and (Test-Path -LiteralPath $installed -PathType Leaf)) {
        $merge = Merge-LocationFiles (Get-Content -LiteralPath $source -Raw) (Get-Content -LiteralPath $installed -Raw)
        [IO.File]::WriteAllText($target, $merge.Json, (New-Object Text.UTF8Encoding($false)))
        if ($merge.Added.Count -gt 0) { Write-Host "locations: added $($merge.Added.Count) to the installed file: $($merge.Added -join ', ')" }
    }
    elseif ([IO.Path]::GetFullPath($source) -ne [IO.Path]::GetFullPath($target)) {
        Copy-Item -LiteralPath $source -Destination $target
    }
    $baseSha = if (Test-Path -LiteralPath $installed -PathType Leaf) { (Get-FileHash -LiteralPath $installed -Algorithm SHA256).Hash } else { $null }
    $script:entries += [ordered]@{
        path = $relativePath
        sha256 = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
        baseSha256 = $baseSha
        policy = $(if ($policy -eq 'merge-defaults' -or $policy -eq 'merge-locations' -or $policy -eq 'patch-weaponinfo') { 'replace' } else { $policy })
    }
}

$entries = @()
Stage-File (Join-Path $snapshot 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll') 'scripts\LibertyFramework.net.dll' 'replace'
Stage-File (Join-Path $snapshot 'native\LibertyCore\bin\LibertyCore.dll') 'scripts\LibertyFramework\bin\LibertyCore.dll' 'replace'
# Liberty SDK next to GTAIV.exe: ScriptHookDotNet loads script assemblies from bytes (Assembly.Load(byte[])) into a domain whose
# ApplicationBase is the game folder, so that is the only place the engine's (and every mod's) Liberty.Sdk reference is probed.
# SDK-only mods go where the engine discovers them.
Stage-File (Join-Path $snapshot 'sdk\Liberty.Sdk\bin\Liberty.Sdk.dll') 'Liberty.Sdk.dll' 'replace'
if (Test-Path -LiteralPath (Join-Path $snapshot 'mods')) {
    Get-ChildItem -LiteralPath (Join-Path $snapshot 'mods') -Directory | ForEach-Object {
        $modDll = Join-Path $_.FullName ('bin\' + $_.Name + '.dll')
        if (Test-Path -LiteralPath $modDll) { Stage-File $modDll "scripts\LibertyFramework\mods\$($_.Name).dll" 'replace' }
    }
}
foreach ($name in @('gunplay.json', 'combat_effects.json', 'weapon-catalog.json', 'atmosphere.json', 'engine.json')) {
    Stage-File (Join-Path $snapshot "config\$name") "scripts\LibertyFramework\config\$name" 'replace'
}
Stage-File (Join-Path $snapshot 'config\arsenal.json') 'scripts\LibertyFramework\config\arsenal.json' 'merge-defaults'
Stage-File (Join-Path $snapshot 'config\holsters.json') 'scripts\LibertyFramework\config\holsters.json' 'merge-defaults'
# T-040: teleport locations (DevTools, the autopilot's `goto`): the owner's file stays, missing ids are appended.
Stage-File (Join-Path $snapshot 'config\devtools\locations.json') 'scripts\LibertyFramework\config\devtools\locations.json' 'merge-locations'
# T-041: the Stage 1 arsenal's identity stats (fire rate, damage, clip, ammo) written into the installed WeaponInfo.xml.
Stage-File (Join-Path $snapshot 'config\weapon-catalog.json') 'update\common\data\WeaponInfo.xml' 'patch-weaponinfo'
# T-033: where the static world objects stand (read by the world mod, mods/Liberty.World: config\<module id>\objects.json).
Stage-File (Join-Path $snapshot 'config\world\objects.json') 'scripts\LibertyFramework\config\world\objects.json' 'replace'
Get-ChildItem -LiteralPath (Join-Path $snapshot 'config\presets') -Filter '*.json' | Sort-Object Name | ForEach-Object {
    Stage-File $_.FullName "scripts\LibertyFramework\config\presets\$($_.Name)" 'replace'
}

Stage-File (Join-Path $built 'extras\vehicle_extras.json') 'scripts\LibertyFramework\config\vehicle_extras.json' 'replace'
Get-ChildItem -LiteralPath (Join-Path $built 'icons') -Filter '*.png' | Sort-Object Name | ForEach-Object {
    Stage-File $_.FullName "scripts\LibertyFramework\ui\icons\$($_.Name)" 'replace'
}
if (Test-Path -LiteralPath (Join-Path $built 'lvs\LibertyVehicleServicesCE.CS')) {
    Stage-File (Join-Path $built 'lvs\LibertyVehicleServicesCE.CS') 'scripts\LibertyVehicleServicesCE.CS' 'replace'
}
Stage-File (Join-Path $built 'models\LibertyModels.img') 'update\LibertyFramework\LibertyModels.img' 'replace'
Stage-File (Join-Path $built 'models\lf_models.ide') 'update\common\data\lf_models.ide' 'replace'
$installedDat = Join-Path $game 'update\common\data\default.dat'
if (-not (Test-Path -LiteralPath $installedDat -PathType Leaf)) { throw 'update\common\data\default.dat missing; install Phase 1 first.' }
$datLines = [IO.File]::ReadAllLines($installedDat)
if (-not ($datLines | Where-Object { $_.Trim() -ieq 'IDE common:/data/lf_models.ide' })) {
    $out = New-Object System.Collections.Generic.List[string]
    $added = $false
    foreach ($line in $datLines) {
        $out.Add($line)
        if (-not $added -and $line.Trim() -ieq 'IDE common:/data/lf_finishes.ide') { $out.Add('IDE common:/data/lf_models.ide'); $added = $true }
    }
    if (-not $added) { throw 'default.dat has no lf_finishes.ide line to anchor lf_models.ide.' }
    $datLines = $out.ToArray()
}
if (Test-Path -LiteralPath (Join-Path $built 'content\LibertyContent.img')) {
    Stage-File (Join-Path $built 'content\LibertyContent.img') 'update\LibertyFramework\LibertyContent.img' 'replace'
    Stage-File (Join-Path $built 'content\lf_content.ide') 'update\common\data\lf_content.ide' 'replace'
    if (-not ($datLines | Where-Object { $_.Trim() -ieq 'IDE common:/data/lf_content.ide' })) {
        $out = New-Object System.Collections.Generic.List[string]
        $added = $false
        foreach ($line in $datLines) {
            $out.Add($line)
            if (-not $added -and $line.Trim() -ieq 'IDE common:/data/lf_models.ide') { $out.Add('IDE common:/data/lf_content.ide'); $added = $true }
        }
        if (-not $added) { throw 'default.dat has no lf_models.ide line to anchor lf_content.ide.' }
        $datLines = $out.ToArray()
    }
}
$work = Join-Path $stage '_work'
New-Item -ItemType Directory -Force -Path $work | Out-Null
$stagedDat = Join-Path $work 'default.dat'
[IO.File]::WriteAllLines($stagedDat, $datLines, (New-Object Text.ASCIIEncoding))
Stage-File $stagedDat 'update\common\data\default.dat' 'replace'
Remove-Item -LiteralPath $work -Recurse -Force

$manifest = [ordered]@{
    package = 'liberty-framework-phase2'
    builtUtc = (Get-Date).ToUniversalTime().ToString('o')
    gameVersion = '1.2.0.59'
    # The worktree and commit the build came from; install-phase2 records them as the installed build.
    source = [ordered]@{ repo = [string]$info.repo; commit = [string]$info.commit; dirty = [bool]$info.dirty; repositories = $info.repositories }
    files = $entries
}
[IO.File]::WriteAllText((Join-Path $stage 'manifest.json'), ($manifest | ConvertTo-Json -Depth 9), (New-Object Text.UTF8Encoding($false)))
Write-Host "Phase 2 staged $($entries.Count) files in $stage"
