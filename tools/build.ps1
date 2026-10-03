param(
    [string] $FrameworkDirectory = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'GTAIV-Reborn'),
    [Parameter(Mandatory = $true)][string] $ScriptHookDotNetReference,
    [switch] $SkipFrameworkBuild,
    [switch] $AllowFrameworkChanges,
    [string] $SourceDirectory,
    [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
$modRoot = Split-Path -Parent $PSScriptRoot
$framework = (Resolve-Path -LiteralPath $FrameworkDirectory).Path
$lockPath = Join-Path $modRoot 'framework.lock.json'
if (-not (Test-Path -LiteralPath $lockPath) -and (Test-Path -LiteralPath (Join-Path $framework 'framework.lock.json'))) {
    $lockPath = Join-Path $framework 'framework.lock.json'
}
if (Test-Path -LiteralPath $lockPath) {
    $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
    $actualJson = & python (Join-Path $framework 'tools\repository\fingerprint.py') --root $framework
    if ($LASTEXITCODE -ne 0) { throw 'Could not fingerprint framework source.' }
    $actual = $actualJson | ConvertFrom-Json
    if ($lock.sourceSha256 -ne $actual.sourceSha256 -and -not $AllowFrameworkChanges) {
        throw 'Framework contract differs from framework.lock.json. Revalidate an intentional update; development builds may explicitly pass -AllowFrameworkChanges.'
    }
}
if (-not $SourceDirectory) { $SourceDirectory = Join-Path $modRoot 'src\LibertyPlus' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $modRoot 'bin' }
if (-not $SkipFrameworkBuild) {
    & (Join-Path $framework 'tools\build.ps1') -ScriptHookDotNetReference $ScriptHookDotNetReference
    if ($LASTEXITCODE -ne 0) { throw 'Framework build failed.' }
}
. (Join-Path $framework 'tools\toolchains.ps1')
$compiler = Join-Path (Get-LibertyToolchain 'roslyn') 'csc.exe'
$sdk = Join-Path $framework 'sdk\Liberty.Sdk\bin\Liberty.Sdk.dll'
$engine = Join-Path $framework 'src\LibertyFramework\bin\Release\LibertyFramework.net.dll'
$reference = (Resolve-Path -LiteralPath $ScriptHookDotNetReference).Path
$sources = @(Get-ChildItem -LiteralPath $SourceDirectory -Recurse -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Sort-Object FullName | ForEach-Object { $_.FullName })
if (-not $sources.Count) { throw 'Liberty+ has no sources.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
Import-Module (Join-Path $framework 'tools\BuildCache.psm1') -Force
$output = Join-Path $OutputDirectory 'LibertyPlus.dll'
$stamp = Join-Path $OutputDirectory '.build-inputs'
$key = Get-InputKey $SourceDirectory ($sources + @($PSCommandPath, $sdk, $engine, $reference)) @("compiler|$(Get-FileIdentity $compiler)")
if (-not (Test-BuildStamp $stamp $key @($output))) {
    Clear-BuildStamp $stamp
    Invoke-LibertyManaged $compiler /nologo /target:library /platform:x86 /optimize+ /unsafe /langversion:7.3 /warn:4 /warnaserror+ "/out:$output" "/reference:$sdk" "/reference:$engine" "/reference:$reference" /reference:System.Core.dll /reference:System.Runtime.Serialization.dll /reference:System.Xml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $sources
    if ($LASTEXITCODE -ne 0) { throw 'Liberty+ compilation failed.' }
    Set-BuildStamp $stamp $key
}
Write-Host "Built Liberty+ $output ($($sources.Count) source files)"
