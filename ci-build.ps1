param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$targetFramework
)

#$env:DOTNET_ROOT = "/usr/share/dotnet"
#$env:PATH = "$env:DOTNET_ROOT" + [System.IO.Path]::PathSeparator + $env:PATH

# Confirm dotnet version.
dotnet --version

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"


# Building for a specific framework.
dotnet build -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
