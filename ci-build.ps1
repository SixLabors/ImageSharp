param(
  [Parameter(Mandatory, Position = 0)]
  [string]$version,
  [Parameter(Mandatory = $true, Position = 1)]
  [string]$targetFramework
)

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
dotnet build -c Release -f $targetFramework /p:packageversion=$version /p:RepositoryUrl=$repositoryUrl
