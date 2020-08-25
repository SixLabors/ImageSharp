param(
  [Parameter(Mandatory = $true, Position = 0)]
  [string]$targetFramework
)

dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
dotnet build tests/ImageSharp.Tests/ImageSharp.Tests.csproj -c Release -f $targetFramework /p:RepositoryUrl=$repositoryUrl
