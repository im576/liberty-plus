# tools/PackageMerge.psm1: how package-phase2.ps1 merges files the owner's install already has.
Import-Module (Join-Path $script:RepoRoot 'tools/PackageMerge.psm1') -Force

$template = '{ "schemaVersion": 1, "locations": [ { "id": "gun_test_range", "x": 1041.0, "y": -568.3, "z": 20.0, "heading": 0.0, "snap": "pavement" }, { "id": "s1_boabo", "x": 890.8, "y": 416.8, "z": 13.1, "heading": 296.0, "snap": "none" }, { "id": "s1_east_hook", "x": 833.3, "y": -157.0, "z": 6.0, "heading": 335.0, "snap": "none" } ] }'
$installed = '{ "schemaVersion": 1, "locations": [ { "id": "gun_test_range", "x": 1.5, "y": 2.5, "z": 3.5, "heading": 90.0, "snap": "none" }, { "id": "my_spot", "x": 5.0, "y": 6.0, "z": 7.0, "heading": 0.0, "snap": "none" } ] }'
$merge = Merge-LocationFiles $template $installed
$result = $merge.Json | ConvertFrom-Json
$byId = @{}; foreach ($l in @($result.locations)) { $byId[[string]$l.id] = $l }
Test-That 'locations merge: missing capture points are added' ($merge.Added.Count -eq 2 -and $byId.ContainsKey('s1_boabo') -and $byId.ContainsKey('s1_east_hook'))
Test-That 'locations merge: the owner''s saved gun test range is kept, not overwritten' ($byId['gun_test_range'].x -eq 1.5 -and $byId['gun_test_range'].heading -eq 90.0 -and $byId['gun_test_range'].snap -eq 'none')
Test-That 'locations merge: the owner''s own entries are kept' ($byId.ContainsKey('my_spot') -and @($result.locations).Count -eq 4)
$again = Merge-LocationFiles $template $merge.Json
Test-That 'locations merge: merging again adds nothing (idempotent)' ($again.Added.Count -eq 0 -and @(($again.Json | ConvertFrom-Json).locations).Count -eq 4)
$single = Merge-LocationFiles '{ "schemaVersion": 1, "locations": [ { "id": "a", "x": 1, "y": 2, "z": 3, "heading": 0, "snap": "none" } ] }' '{ "schemaVersion": 1, "locations": [] }'
Test-That 'locations merge: a one-entry result is still a JSON array' ($single.Json -match '"locations":\s*\[') $single.Json
$threw = $false
try { Merge-LocationFiles '{ "schemaVersion": 2, "locations": [] }' $installed | Out-Null } catch { $threw = $true }
Test-That 'locations merge: a different schemaVersion is refused, not merged' $threw

# Merge-WeaponInfoStats (T-041): the catalog's identity stats reach WeaponInfo.xml, and only them.
$weaponXml = @'
<?xml version="1.0" encoding="utf-8"?>
<weaponinfo version="1">
  <weapon type="PISTOL">
    <data slot="HANDGUN" firetype="INSTANT_HIT" clipsize="17" ammomax="425" timebetweenshots="333">
      <damage base="60" networkplayermod="3.0" />
      <aiming accuracy="0.5" />
    </data>
    <assets model="w_glock"></assets>
  </weapon>
  <weapon type="MICRO_UZI">
    <data slot="SMG" firetype="INSTANT_HIT" clipsize="30" ammomax="300" timebetweenshots="66">
      <damage base="55" networkplayermod="3.0" />
      <aiming accuracy="0.55" />
    </data>
    <assets model="w_uzi"></assets>
  </weapon>
</weaponinfo>
'@
$statsCatalog = '{ "schemaVersion": 1, "applyWeaponInfoStats": true, "entries": [ { "id": "service-pistol", "stage1": true, "weaponInfoType": "PISTOL", "stats": { "timeBetweenShotsMilliseconds": 333, "damageBase": 60, "clipSize": 17, "ammoMax": 425 }, "vanillaStats": { "timeBetweenShotsMilliseconds": 333, "damageBase": 60, "clipSize": 17, "ammoMax": 425 } }, { "id": "imi-uzi", "stage1": true, "weaponInfoType": "MICRO_UZI", "stats": { "timeBetweenShotsMilliseconds": 85, "damageBase": 45 }, "vanillaStats": { "timeBetweenShotsMilliseconds": 66, "damageBase": 55 } }, { "id": "gold", "stage1": false, "weaponInfoType": "LF_GOLD_PISTOL", "stats": { "damageBase": 1 } } ] }'
$patched = Merge-WeaponInfoStats $statsCatalog $weaponXml
$patchedDocument = [xml]$patched.Xml
$uzi = $patchedDocument.SelectSingleNode("/weaponinfo/weapon[@type='MICRO_UZI']")
$pistol = $patchedDocument.SelectSingleNode("/weaponinfo/weapon[@type='PISTOL']")
Test-That 'weaponinfo stats: the catalog values are written into the weapon entry' ($uzi.data.timebetweenshots -eq '85' -and $uzi.data.damage.base -eq '45') $patched.Xml
Test-That 'weaponinfo stats: fields the catalog does not set stay as they were' ($uzi.data.clipsize -eq '30' -and $uzi.data.ammomax -eq '300' -and $uzi.data.aiming.accuracy -eq '0.55' -and $uzi.data.damage.networkplayermod -eq '3.0')
Test-That 'weaponinfo stats: an entry that already matches is not reported' ($patched.Changes.Count -eq 2 -and $pistol.data.timebetweenshots -eq '333')
Test-That 'weaponinfo stats: a non-Stage 1 catalog entry is never written' ($patched.Xml -notmatch 'LF_GOLD_PISTOL')
Test-That 'weaponinfo stats: the file stays UTF-8 with its whitespace' ($patched.Xml.StartsWith('<?xml version="1.0" encoding="utf-8"?>') -and $patched.Xml -match '\r?\n  <weapon type="MICRO_UZI">')
$again = Merge-WeaponInfoStats $statsCatalog $patched.Xml
Test-That 'weaponinfo stats: applying twice changes nothing and returns the text unchanged' ($again.Changes.Count -eq 0 -and $again.Xml -ceq $patched.Xml)
$off = Merge-WeaponInfoStats ($statsCatalog -replace '"applyWeaponInfoStats": true', '"applyWeaponInfoStats": false') $patched.Xml
$offUzi = ([xml]$off.Xml).SelectSingleNode("/weaponinfo/weapon[@type='MICRO_UZI']")
Test-That 'weaponinfo stats: switched off, the game''s own values come back' ($offUzi.data.timebetweenshots -eq '66' -and $offUzi.data.damage.base -eq '55')
$missingWeapon = $false
try { Merge-WeaponInfoStats ($statsCatalog -replace 'MICRO_UZI', 'NO_SUCH_GUN') $weaponXml | Out-Null } catch { $missingWeapon = $true }
Test-That 'weaponinfo stats: a catalog weapon the XML lacks is an error, not a silent skip' $missingWeapon
