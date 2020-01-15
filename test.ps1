param(
  [Parameter(Mandatory, Position = 0)]
  [string]$os,
  [Parameter(Mandatory, Position = 1)]
  [string]$targetFramework,
  [Parameter(Mandatory, Position = 2)]
  [string]$platform,
  [Parameter(Mandatory, Position = 3)]
  [bool]$codecov
)

if ($codecov -eq $TRUE) {

  # xunit doesn't understand the CollectCoverage params
  dotnet clean -c Debug
  dotnet test -c Debug -f $targetFramework /p:codecov=true /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput='../../../coverage.xml'
}
else {

  # There were issues matching the correct installed runtime if we do not specify it explicitly:
  # https://github.com/xunit/xunit/issues/1476
  # This fix assumes the base version is installed.
  $coreTargetFrameworkRegex = '^netcoreapp(\d+\.\d+)$'
  if ($targetFramework -match $coreTargetFrameworkRegex) {
    $fxVersion = "--fx-version ${matches[1]}.0"
  }

  Set-Location $env:XUNIT_PATH

  dotnet clean -c Release
  dotnet xunit -c Release -f $targetFramework ${fxVersion} $platform

  Set-Location $PSScriptRoot
}
