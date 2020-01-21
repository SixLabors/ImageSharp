param(
  [Parameter(Mandatory, Position = 0)]
  [string]$version,
  [Parameter(Mandatory = $false, Position = 1)]
  [string]$targetFramework = 'ALL'
)

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"
if ($targetFramework -ne 'ALL') {

  # Building for a specific framework.
  dotnet build -c Release -f $targetFramework /p:packageversion=$version /p:RepositoryUrl=$repositoryUrl
}
else {

  # Building for packing and publishing.
  dotnet pack -c Release --output "$PSScriptRoot/artifacts" /p:packageversion=$version /p:RepositoryUrl=$repositoryUrl
}
