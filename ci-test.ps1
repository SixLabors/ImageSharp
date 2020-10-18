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

if ($codecov -eq 'true') {

  # Allow toggling of profile to workaround any potential JIT errors caused by code injection.
  dotnet clean -c $codecovProfile
  dotnet test --collect "XPlat Code Coverage" --settings .\tests\coverlet.runsettings -c $codecovProfile -f $targetFramework /p:CodeCov=true
}
elseif ($os -eq 'windows-latest' -and  $platform -eq 'x86') {

  # Only Windows supports setting the target platform for now.
  dotnet test --no-build -c Release -f $targetFramework -- RunConfiguration.TargetPlatform=$platform
}
else {

  # Default to 64 bit
  dotnet test --no-build -c Release -f $targetFramework
}
