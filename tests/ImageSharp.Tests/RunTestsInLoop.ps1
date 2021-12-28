# This script can be used to collect logs from sporadic bugs
Param(
    [int]$TestRunCount=10,
    [string]$TargetFramework="netcoreapp3.1",
    [string]$Configuration="Release"
)

$runId = Get-Random -Minimum 0 -Maximum 9999

dotnet build -c $Configuration -f $TargetFramework
for ($i = 0; $i -lt $TestRunCount; $i++) {
    $logFile = ".\_testlog-" + $runId.ToString("d4") + "-run-" + $i.ToString("d3") + ".log"
    Write-Host "Test run $i ..."
    & dotnet test --no-build -c $Configuration -f $TargetFramework 3>&1 2>&1 > $logFile
    if ($LastExitCode -eq 0) {
        Write-Host "Success!"
        Remove-Item $logFile
    }
    else {
        Write-Host "Failed: $logFile"
    }
}
