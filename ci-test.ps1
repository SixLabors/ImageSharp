param(
  [Parameter(Mandatory, Position = 0)]
  [string]$os,
  [Parameter(Mandatory, Position = 1)]
  [string]$targetFramework,
  [Parameter(Mandatory, Position = 2)]
  [string]$platform,
  [Parameter(Mandatory, Position = 3)]
  [string]$codecov,
  [Parameter(Position = 4)]
  [string]$codecovProfile = 'Release'
)

$netFxRegex = '^net\d+'

if ($codecov -eq 'true') {

  # Allow toggling of profile to workaround any potential JIT errors caused by code injection.
  dotnet clean -c $codecovProfile
  dotnet test --collect "XPlat Code Coverage" --settings .\tests\coverlet.runsettings -c $codecovProfile -f $targetFramework /p:CodeCov=true
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
else {

  dotnet test --no-build -c Release -f $targetFramework
}
