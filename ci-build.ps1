param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$targetFramework
)

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
# dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
#
# CI is now throwing build errors when none were present previously.
dotnet build -c Release -f /p:RepositoryUrl=$repositoryUrl
