param(
  [Parameter(Mandatory, Position = 0)]
  [string]$os,
  [Parameter(Mandatory, Position = 1)]
  [string]$targetFramework,
  [Parameter(Mandatory, Position = 2)]
  [string]$platform,
  [Parameter(Mandatory, Position = 3)]
  [string]$codecov
)

  $netFxRegex = '^net\d+'

if ($codecov -eq 'true') {

  # xunit doesn't understand custom params so use dotnet test.
  # Coverage tests are run in debug because the coverage tools are triggering a JIT error in filter processors
  # that causes the blue component of transformed values to be corrupted.
  dotnet clean -c Debug
  dotnet test -c Debug -f $targetFramework /p:codecov=true
}
elseif ($platform -eq '-x86' -and $targetFramework -match $netFxRegex) {

  # xunit doesn't run on core with NET SDK 3.1+.
  # xunit doesn't actually understand -x64 as an option.
  #
  # xunit requires explicit path.
  Set-Location $env:XUNIT_PATH

  dotnet xunit --no-build -c Release -f $targetFramework ${fxVersion} $platform

  Set-Location $PSScriptRoot
}
else  {

  dotnet test --no-build -c Release -f $targetFramework
}
