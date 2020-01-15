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

if ($codecov -eq 'true') {

  # xunit doesn't understand custom params so use dotnet test
  # Coverage tests are run in debug because the coverage tools are triggering a JIT error in filter processors
  # that causes the blue component of transformed values to be corrupted.
  dotnet clean -c Debug
  dotnet test -c Debug -f $targetFramework /p:codecov=true
}
else {

  # There were issues matching the correct installed runtime if we do not specify it explicitly:
  # https://github.com/xunit/xunit/issues/1476
  # This fix assumes the base version is installed.
  $coreTargetFrameworkRegex = '^netcoreapp(\d+\.\d+)$'
  if ($targetFramework -match $coreTargetFrameworkRegex) {
    $fxVersion = "--fx-version ${matches[1]}.0"
  }

  # xunit requires explicit path
  Set-Location $env:XUNIT_PATH

  # xunit doesn't actually understand -x64 as an option
  if ($platform -ne '-x86') {
    $platform = ''
  }

  dotnet clean -c Release
  dotnet xunit -c Release -f $targetFramework ${fxVersion} $platform

  Set-Location $PSScriptRoot
}
