param(
    [string]$targetFramework,
    [string]$is32Bit = "False"
)

if (!$targetFramework){
    Write-Host "run-tests.ps1 ERROR: targetFramework is undefined!"
    exit 1
}

if ( ($targetFramework -eq "netcoreapp2.0") -and ($env:CI -eq "True") -and ($is32Bit -ne "True")) {
    # We execute CodeCoverage.cmd only for one specific job on CI (netcoreapp2.0 + 64bit )
    $testRunnerCmd = ".\tests\CodeCoverage\CodeCoverage.cmd"
}
elseif ($targetFramework -eq "mono") {
    $testRunnerCmd = "Write-Host '**** placeholder for mono test execution ****'"
}
else {
    cd .\tests\ImageSharp.Tests
    $xunitArgs = "-c Release -framework $targetFramework"

    if ($targetFramework -eq "netcoreapp2.0") {
        # There are issues matching the correct installed runtime if we do not specify it explicitly:
        $xunitArgs += " --fx-version 2.0.0"
    }

    if ($is32Bit -eq "True") {
        $xunitArgs += " -x86"
    }

    $testRunnerCmd = "dotnet xunit $xunitArgs"
}

Write-Host "running:"
Write-Host $testRunnerCmd
Write-Host "..."

Invoke-Expression $testRunnerCmd

cd $PSScriptRoot