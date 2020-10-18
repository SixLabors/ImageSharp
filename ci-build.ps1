param(
  [Parameter(Mandatory, Position = 0)]
  [string]$os,
  [Parameter(Mandatory, Position = 1)]
  [string]$targetFramework,
  [Parameter(Mandatory, Position = 2)]
  [string]$platform
)

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

if ($os -eq 'windows-latest' -and  $platform -eq 'x86') {

  # Only Windows supports setting the target platform for now.
  dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl -- RunConfiguration.TargetPlatform=$platform
}
else {

  # Default to 64 bit
  dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
}
