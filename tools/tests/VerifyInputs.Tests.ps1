Import-Module (Join-Path $script:RepoRoot 'tools/local/VerifyLocal.psm1') -Force
$stageProbeRoot = Join-Path $script:Scratch 'verify-stage-prerequisite'
New-Item -ItemType Directory -Force $stageProbeRoot | Out-Null
$stageProbe = & (Get-Module VerifyLocal) {
    param($probeRoot)
    $savedChild = ${function:Invoke-ChildProcess}
    $savedTool = ${function:Get-ToolCommand}
    $script:stageProbeCalls = 0
    $script:stageProbeVerifyCalls = 0
    $script:stageProbeFail = $false
    $script:stageProbeInput = Join-Path $probeRoot 'generated.xml'
    function Invoke-ChildProcess($File, $Arguments, $Timeout, $Log, $Directory) {
        if ($File -eq 'verify-probe') { $script:stageProbeVerifyCalls++; return @{ ExitCode = 0; TimedOut = $false; Output = 'verified' } }
        if ($Arguments -notcontains 'Stage') { throw 'unexpected child in stage prerequisite test' }
        $script:stageProbeCalls++
        if ($script:stageProbeFail) { return @{ ExitCode = 1; TimedOut = $false; Output = 'stage failed' } }
        [IO.File]::WriteAllText($script:stageProbeInput, '<generated />')
        return @{ ExitCode = 0; TimedOut = $false; Output = 'staged' }
    }
    function Get-ToolCommand($Context, $Check) {
        if ($Context.Installed -or -not (Test-Path -LiteralPath $script:stageProbeInput)) { throw 'verify ran before stage or after installation' }
        return @{ File = 'verify-probe'; Arguments = @(); Timeout = 1; Pass = '' }
    }
    try {
        $package = [pscustomobject]@{ id = 'LOOP-package-install'; run = [pscustomobject]@{ tool = 'package-install' } }
        $verify = [pscustomobject]@{ id = 'LOOP-verify'; kind = 'pc-offline'; run = [pscustomobject]@{ tool = 'verify' } }
        $context = @{ Repo = $probeRoot; Game = $probeRoot; Results = $probeRoot; Shdn = 'reference'; Simulate = $false; Installed = $false;
            PackageBuild = @{ Ok = $true }; SelectedPackageCheck = $package }
        $result = Invoke-ToolCheck $context $verify
        Invoke-PackageStage $context $package
        $success = $result.Status -eq 'PASS' -and $script:stageProbeCalls -eq 1 -and $script:stageProbeVerifyCalls -eq 1 -and -not $context.Installed
        $script:stageProbeFail = $true
        $context.Remove('PackageStage')
        $result = Invoke-ToolCheck $context $verify
        $failedInstall = Invoke-PackageInstall $context $package
        $failure = $result.Status -eq 'NOT-RUN' -and $failedInstall.Status -eq 'FAIL' -and
            $script:stageProbeVerifyCalls -eq 1 -and $script:stageProbeCalls -eq 2 -and -not $context.Installed
        return @{ Success = $success; Failure = $failure }
    }
    finally {
        ${function:Invoke-ChildProcess} = $savedChild
        ${function:Get-ToolCommand} = $savedTool
    }
} $stageProbeRoot
Test-That 'generated-input verifier: stage runs once before verification and installation' $stageProbe.Success
Test-That 'generated-input verifier: failed stage is not verified or installed, and package failure remains' $stageProbe.Failure

