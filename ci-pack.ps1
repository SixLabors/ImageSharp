dotnet clean -c Release

$repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"

# Building for packing and publishing.
dotnet pack -c Release --output "$PSScriptRoot/artifacts" /p:RepositoryUrl=$repositoryUrl
