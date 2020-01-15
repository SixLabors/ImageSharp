param(
  [string]$os,
  [string]$targetFramework,
  [string]$doCoverage = "False",
  [string]$is32Bit = "False"
)

if (!$os) {
  Write-Host "run-tests.ps1 ERROR: os is undefined!"
  exit 1
}

if (!$targetFramework) {
  Write-Host "run-tests.ps1 ERROR: targetFramework is undefined!"
  exit 1
}

function VerifyPath($path, $errorMessage) {
  if (!(Test-Path -Path $path)) {
    Write-Host "run-tests.ps1 $errorMessage `n $xunitRunnerPath"
    exit 1
  }
}

function CheckSubmoduleStatus() {
  $submoduleStatus = (git submodule status) | Out-String
  # if the result string is empty, the command failed to run (we didn't capture the error stream)
  if ($submoduleStatus) {
    # git has been called successfully, what about the status?
    if (($submoduleStatus -match "\-") -or ($submoduleStatus -match "\(\(null\)\)")) {
      # submodule has not been initialized!
      return 2;
    }
    elseif ($submoduleStatus -match "\+") {
      # submodule is not synced:
      return 1;
    }
    else {
      # everything fine:
      return 0;
    }
  }
  else {
    # git call failed, so we should warn
    return 3;
  }
}

if (($os -eq "windows-latest") -and ($doCoverage -eq "True") -and ($env:CI -eq "True") -and ($is32Bit -ne "True")) {
  # We execute CodeCoverage.cmd only for one specific job on CI (windows + coverageTargetFramework + 64bit )
  $testRunnerCmd = ".\tests\CodeCoverage\CodeCoverage.cmd"
}
else {
  Set-Location .\tests
  $xunitArgs = "-nobuild -c Release -framework $targetFramework"

  $coreTargetFrameworkRegex = '^netcoreapp(\d+\.\d+)$'
  if ($targetFramework -match $coreTargetFrameworkRegex) {
    # There were issues matching the correct installed runtime if we do not specify it explicitly:
    $fxVersion = $matches[1] + ".0"
    $xunitArgs += " --fx-version $fxVersion"
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

Set-Location $PSScriptRoot

$exitCodeOfTests = $LASTEXITCODE;

if (0 -ne ([int]$exitCodeOfTests)) {
  # check submodule status
  $submoduleStatus = CheckSubmoduleStatus
  if ([int]$submoduleStatus -eq 1) {
    # not synced
    Write-Host -ForegroundColor Yellow "Check if submodules are up to date. You can use 'git submodule update' to fix this";
  }
  elseif ($submoduleStatus -eq 2) {
    # not initialized
    Write-Host -ForegroundColor Yellow "Check if submodules are initialized. You can run 'git submodule init' to initialize them."
  }
  elseif ($submoduleStatus -eq 3) {
    # git not found, maybe submodules not synced?
    Write-Host -ForegroundColor Yellow "Could not check if submodules are initialized correctly. Maybe git is not installed?"
  }
  else {
    #Write-Host "Submodules are up to date";
  }
}

exit $exitCodeOfTests
