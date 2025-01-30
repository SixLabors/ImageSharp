param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$targetFramework
)

Write-Output $env:PATH

dotnet --list-sdks

# Confirm dotnet version.
dotnet --version

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
