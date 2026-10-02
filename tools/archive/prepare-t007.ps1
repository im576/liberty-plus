param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$base = Join-Path $game 'common\data\WeaponInfo.xml'
$stageDirectory = Join-Path $repoRoot 'staging\t007'
$stageXml = Join-Path $stageDirectory 'WeaponInfo.xml'
$stageHash = Join-Path $stageDirectory 'base.sha256'
if (-not (Test-Path -LiteralPath $base)) { throw "Base weapon data not found: $base" }

$document = New-Object System.Xml.XmlDocument
$document.PreserveWhitespace = $true
$document.Load($base)
$candidates = @(
    @{ Vanilla = 'PISTOL'; Custom = 'LF_GOLD_PISTOL' },
    @{ Vanilla = 'M4'; Custom = 'LF_GOLD_CARBINE' },
    @{ Vanilla = 'SHOTGUN'; Custom = 'LF_GOLD_SHOTGUN' }
)
foreach ($entry in $candidates) {
    $vanilla = $document.SelectSingleNode("/weaponinfo/weapon[@type='$($entry.Vanilla)']")
    if ($null -eq $vanilla) { throw "Vanilla $($entry.Vanilla) definition not found." }
    if ($document.SelectSingleNode("/weaponinfo/weapon[@type='$($entry.Custom)']")) {
        throw "Candidate name already exists in the base weapon data: $($entry.Custom)"
    }
    $candidate = $vanilla.CloneNode($true)
    $candidate.Attributes['type'].Value = $entry.Custom
    $null = $document.DocumentElement.AppendChild($candidate)
}
New-Item -ItemType Directory -Force -Path $stageDirectory | Out-Null
$document.Save($stageXml)
$baseDigest = (Get-FileHash -LiteralPath $base -Algorithm SHA256).Hash
Set-Content -LiteralPath $stageHash -Value $baseDigest -NoNewline
Write-Host "Staged T-007 data: $stageXml"
Write-Host 'Three candidates reuse the vanilla pistol, M4, and pump shotgun models/stats for identity testing only.'
