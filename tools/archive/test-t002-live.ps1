param(
    [Parameter(Mandatory = $true)]
    [string] $GameDirectory,
    [int] $TimeoutSeconds = 90
)

$ErrorActionPreference = 'Stop'
$game = (Resolve-Path -LiteralPath $GameDirectory).Path
$exe = Join-Path $game 'GTAIV.exe'
$configPath = Join-Path $game 'scripts\LibertyFramework\config\probe.json'
$logPath = Join-Path $game 'scripts\LibertyFramework\logs\LibertyFramework.log'

if (-not (Test-Path -LiteralPath $exe)) { throw "GTAIV.exe not found in $game" }
if (-not (Test-Path -LiteralPath $configPath)) { throw "Probe config not found: $configPath" }
if (-not (Test-Path -LiteralPath $logPath)) { throw "Project log not found: $logPath" }
if ($TimeoutSeconds -lt 15) { throw 'TimeoutSeconds must be at least 15.' }

function Assert-GameRunning {
    $running = Get-Process GTAIV -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq $exe }
    if (-not $running) { throw 'GTA IV is not running from the selected game directory.' }
}

function Read-Log {
    $stream = [IO.File]::Open($logPath, [IO.FileMode]::Open, [IO.FileAccess]::Read,
        [IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object IO.StreamReader($stream)
        try { return $reader.ReadToEnd() }
        finally { $reader.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Wait-LogPattern([string] $description, [string] $pattern, [int] $startLength) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        Assert-GameRunning
        $log = Read-Log
        $fresh = if ($log.Length -ge $startLength) { $log.Substring($startLength) } else { $log }
        if ([regex]::IsMatch($fresh, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)) {
            Write-Host "PASS: $description"
            return
        }
        Start-Sleep -Seconds 1
    }
    throw "Timed out waiting for $description. Inspect $logPath"
}

function Write-Probe([string] $contents) {
    [IO.File]::WriteAllText($configPath, $contents, (New-Object System.Text.UTF8Encoding -ArgumentList $false))
}

Assert-GameRunning
$originalBytes = [IO.File]::ReadAllBytes($configPath)
$suffix = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$changed = "changed_$suffix"
$restored = "restored_$suffix"
try {
    $start = (Read-Log).Length
    Write-Probe "{`"schemaVersion`":1,`"probeLabel`":`"$changed`"}"
    $validPattern = 'config_loaded[^\r\n]*probe_label=' + [regex]::Escape($changed) +
        '.*heartbeat probe_label=' + [regex]::Escape($changed)
    Wait-LogPattern 'valid config reload and heartbeat' $validPattern $start

    $start = (Read-Log).Length
    Write-Probe '{broken json'
    $badPattern = 'config_reload_failed[^\r\n]*retained_label=' + [regex]::Escape($changed) +
        '.*heartbeat probe_label=' + [regex]::Escape($changed)
    Wait-LogPattern 'malformed config retained the prior label' $badPattern $start

    $start = (Read-Log).Length
    Write-Probe "{`"schemaVersion`":1,`"probeLabel`":`"$restored`"}"
    $recoveryPattern = 'config_loaded[^\r\n]*probe_label=' + [regex]::Escape($restored) +
        '.*heartbeat probe_label=' + [regex]::Escape($restored)
    Wait-LogPattern 'valid config recovery and heartbeat' $recoveryPattern $start
}
finally {
    [IO.File]::WriteAllBytes($configPath, $originalBytes)
    Write-Host 'Original probe config restored.'
}
