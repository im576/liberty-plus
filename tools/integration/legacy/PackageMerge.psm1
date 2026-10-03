# Merges used by package-phase2.ps1 when a config file already exists in the owner's install (tests: tools/tests/PackageMerge.Tests.ps1).
# Compatible with Windows PowerShell 5.1 and PowerShell 7.

# config/devtools/locations.json: the installed file is the owner's (DevTools "Set Gun Test Range Here" rewrites it), so it
# is never replaced. Entries of the repository file whose id is missing are appended (new capture points reach an existing
# install); entries that already exist keep the owner's values. Returns the merged JSON text and the ids that were added.
function Merge-LocationFiles([string] $TemplateJson, [string] $InstalledJson) {
    $template = $TemplateJson | ConvertFrom-Json
    $saved = $InstalledJson | ConvertFrom-Json
    if ($saved.schemaVersion -ne $template.schemaVersion) { throw 'Cannot merge locations: schemaVersion differs' }
    $locations = New-Object System.Collections.Generic.List[object]
    $known = @{}
    foreach ($location in @($saved.locations)) { $locations.Add($location); $known[[string]$location.id] = $true }
    $added = New-Object System.Collections.Generic.List[string]
    foreach ($location in @($template.locations)) {
        if ($known.ContainsKey([string]$location.id)) { continue }
        $locations.Add($location)
        $added.Add([string]$location.id)
    }
    $saved.locations = $locations.ToArray()
    return @{ Json = ($saved | ConvertTo-Json -Depth 32); Added = $added.ToArray() }
}


# config/weapon-catalog.json -> the installed update\common\data\WeaponInfo.xml (T-041). Each Stage 1 catalog entry's `stats`
# (`vanillaStats` when the catalog's applyWeaponInfoStats is false, so the switch restores the game's own values) are written
# into its <weapon type=weaponInfoType>: data@timebetweenshots, data@clipsize, data@ammomax and damage@base. Nothing else in
# the file is touched; when nothing changes the original text is returned byte for byte. Returns Xml (the text) and Changes
# ("TYPE field old->new" lines).
function Merge-WeaponInfoStats([string] $CatalogJson, [string] $InstalledXml) {
    $catalog = $CatalogJson | ConvertFrom-Json
    $useStats = [bool]$catalog.applyWeaponInfoStats
    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.XmlResolver = $null
    $document.LoadXml($InstalledXml)
    $fields = @(
        @{ Name = 'timeBetweenShotsMilliseconds'; Node = 'data'; Attribute = 'timebetweenshots' },
        @{ Name = 'damageBase'; Node = 'data/damage'; Attribute = 'base' },
        @{ Name = 'clipSize'; Node = 'data'; Attribute = 'clipsize' },
        @{ Name = 'ammoMax'; Node = 'data'; Attribute = 'ammomax' }
    )
    $changes = New-Object System.Collections.Generic.List[string]
    foreach ($entry in @($catalog.entries)) {
        if (-not $entry.stage1) { continue }
        $wanted = if ($useStats) { $entry.stats } else { $entry.vanillaStats }
        if (-not $wanted) { throw "Catalog entry $($entry.id) has no $(if ($useStats) { 'stats' } else { 'vanillaStats' })" }
        $weapon = $document.SelectSingleNode("/weaponinfo/weapon[@type='$($entry.weaponInfoType)']")
        if (-not $weapon) { throw "WeaponInfo.xml has no weapon $($entry.weaponInfoType) (catalog entry $($entry.id))" }
        foreach ($field in $fields) {
            $value = $wanted.PSObject.Properties[$field.Name]
            if ($null -eq $value -or $null -eq $value.Value) { continue }
            $node = $weapon.SelectSingleNode($field.Node)
            if (-not $node) { throw "WeaponInfo.xml weapon $($entry.weaponInfoType) has no <$($field.Node)>" }
            $text = ([int]$value.Value).ToString([Globalization.CultureInfo]::InvariantCulture)
            $current = $node.GetAttribute($field.Attribute)
            if ($current -eq $text) { continue }
            $node.SetAttribute($field.Attribute, $text)
            $changes.Add("$($entry.weaponInfoType) $($field.Attribute) $(if ($current) { $current } else { '-' })->$text")
        }
    }
    if ($changes.Count -eq 0) { return @{ Xml = $InstalledXml; Changes = @() } }
    # Whitespace is preserved, so only the changed attributes differ; UTF-8 without a byte-order mark (the caller writes the bytes).
    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.Xml.XmlTextWriter($stream, (New-Object System.Text.UTF8Encoding($false)))
    $document.Save($writer)
    $writer.Flush()
    $xml = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
    return @{ Xml = $xml; Changes = $changes.ToArray() }
}
Export-ModuleMember -Function Merge-LocationFiles, Merge-WeaponInfoStats
