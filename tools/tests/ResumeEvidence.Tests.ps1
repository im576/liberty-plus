# Resume must not relabel old evidence or resurrect a restored installation.
$resumeModule = Import-Module (Join-Path $script:RepoRoot 'tools/local/VerifyLocal.psm1') -Force -PassThru
$receipt = [pscustomobject]@{ run = [pscustomobject]@{ commit = 'abcdef1'; mode = 'full'; queueSha256 = 'q1' }
    checks = @([pscustomobject]@{ id = 'LOOP-package-install'; status = 'PASS' }) }
$gameCheck = [pscustomobject]@{ id = 'gore'; kind = 'scenario'; run = [pscustomobject]@{ tool = 'scenario' } }
function Get-ResumeRejection($Old, $Commit, $Mode, $QueueHash, $Selected) {
    & $resumeModule { param($o, $c, $m, $q, $s)
        try { Assert-ResumeIdentity $o $c $m $q $s; return '' } catch { return $_.Exception.Message }
    } $Old $Commit $Mode $QueueHash $Selected
}
Test-That 'resume: changed source cannot inherit passes' ((Get-ResumeRejection $receipt 'bbbbbbb' 'full' 'q1' @()) -like '*different commit*')
Test-That 'resume: quick evidence cannot become full acceptance' ((Get-ResumeRejection $receipt 'abcdef123' 'quick' 'q1' @()) -like '*verification mode*')
Test-That 'resume: edited scenarios queue cannot inherit passes' ((Get-ResumeRejection $receipt 'abcdef123' 'full' 'q2' @()) -like '*check queue*')
Test-That 'resume: historical install never authorizes pending gameplay' ((Get-ResumeRejection $receipt 'abcdef123' 'full' 'q1' @($gameCheck)) -like '*historical install*')
Test-That 'resume: completed evidence can be republished without installing' ((Get-ResumeRejection $receipt 'abcdef123' 'full' 'q1' @()) -eq '')
$receipt.run.queueSha256 = $null
Test-That 'resume: legacy queue identity is unknown, not a pass' ((Get-ResumeRejection $receipt 'abcdef123' 'full' 'q1' @()) -like '*unrecorded check queue*')

# A -> B -> C: all three must be selected even if only A is requested.
$queue = [pscustomobject]@{ checks = @(
    [pscustomobject]@{ id = 'A'; kind = 'probe'; status = 'READY'; needs = @('B'); run = @{ tool = 'probe' } },
    [pscustomobject]@{ id = 'B'; kind = 'probe'; status = 'READY'; needs = @('C'); run = @{ tool = 'probe' } },
    [pscustomobject]@{ id = 'C'; kind = 'probe'; status = 'READY'; run = @{ tool = 'probe' } }) }
$selected = @(Select-Checks $queue @('A') @() $false $false)
Test-That 'queue: transitive prerequisites run before their dependents' (($selected.id -join ',') -eq 'C,B,A')
$selected = @(Select-Checks $queue @('A', 'C') @() $false $false)
Test-That 'queue: explicitly selected shared prerequisites still precede dependents' (($selected.id -join ',') -eq 'C,B,A')
$queue.checks[2] | Add-Member -NotePropertyName needs -NotePropertyValue @('A')
$cycle = ''
try { Select-Checks $queue @('A') @() $false $false | Out-Null } catch { $cycle = $_.Exception.Message }
Test-That 'queue: dependency cycles fail before any work' ($cycle -like '*Cyclic*')

foreach ($outcome in @(@{ ExitCode = 1; TimedOut = $false }, @{ ExitCode = 0; TimedOut = $true })) {
    $restore = @{ Run = @{}; Backup = 'test-backup'; Checks = New-Object System.Collections.ArrayList }
    & $resumeModule { param($c, $r) Set-RestoreResult $c $r } $restore $outcome
    $counts = Get-StatusCounts $restore.Checks
    Test-That "restore: failure is an ERROR result (exit=$($outcome.ExitCode) timeout=$($outcome.TimedOut))" (
        $counts['ERROR'] -eq 1 -and $restore.Run.install -like 'RESTORE FAILED*' -and
        $restore.Checks[0].evidence -contains 'restore.log')
}
$restore = @{ Run = @{}; Backup = 'test-backup'; Checks = New-Object System.Collections.ArrayList }
& $resumeModule { param($c) Set-RestoreResult $c @{ ExitCode = 0; TimedOut = $false } } $restore
Test-That 'restore: success records the exact backup without introducing an error' (
    $restore.Run.install -eq 'restored from test-backup' -and $restore.Checks.Count -eq 0)
