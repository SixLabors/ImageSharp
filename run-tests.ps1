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
    $testDllPath = "$PSScriptRoot\tests\ImageSharp.Tests\bin\Release\net462\SixLabors.ImageSharp.Tests.dll"
    cd "$env:HOMEPATH\.nuget\packages\xunit.runner.console\2.3.1\tools\net452\"
    if ($is32Bit -ne "True") {
        $monoPath = "$env:PROGRAMFILES\Mono\bin\mono.exe"
    }
    else {
        $monoPath = "${env:ProgramFiles(x86)}\Mono\bin\mono.exe"
    }
    
    $testRunnerCmd = '"$monoPath" .\xunit.console.exe $testDllPath'
}
else {
    cd .\tests\ImageSharp.Tests
    $xunitArgs = "-nobuild -c Release -framework $targetFramework"

    if ($targetFramework -eq "netcoreapp2.0") {
        # There were issues matching the correct installed runtime if we do not specify it explicitly:
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