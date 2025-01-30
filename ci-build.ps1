param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$targetFramework
)

Write-Output "PATH"
Write-Output $env:PATH

Write-Output "DOTNET_LIST"
dotnet --list-sdks

# Confirm dotnet version.
Write-Output "DOTNET_VERSION"
dotnet --version

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
