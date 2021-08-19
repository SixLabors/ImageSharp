dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for a specific framework.
dotnet build -c Release /p:RepositoryUrl=$repositoryUrl
