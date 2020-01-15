param(
  [string]$targetFramework = 'ALL'
)

# Lets calculate the correct version here
$fallbackVersion = "1.0.0";
$version = ''

$tagRegex = '^v?(\d+\.\d+\.\d+)(?:-([a-zA-Z]+)\.?(\d*))?$'

function ToBuildNumber {
  param( $date )

  if ("$date" -eq "") {
    $date = [System.DateTime]::Now
  }

  if ($date.GetType().fullname -ne 'System.DateTime') {
    $date = [System.DateTime]::Parse($date)
  }

  return $date.ToString("yyyyMMddhhmmss")
}

# We are running on the build server
$isVersionTag = "$env:GITHUB_REF".replace("refs/tags/", "") -match $tagRegex

if ($isVersionTag) {
  Write-Debug "Github tagged build"
}

if ($isVersionTag -eq $false) {
  if ( "$(git diff --stat)" -eq '') {
    Write-Debug "Clean repo"
    if ("$(git tag --list)" -ne "") {
      Write-Debug "Has tags"
      $tagData = (git describe --tags HEAD)
      $isVersionTag = $tagData -match $tagRegex
      Write-Debug $tagData
    }
  }
  else {
    Write-Debug "Dirty repo"
  }
}

if ($isVersionTag) {

  Write-Debug "Building commit tagged with a compatable version number"

  $version = $matches[1]
  $postTag = $matches[2]
  $count = $matches[3]

  Write-Debug "Version number: ${version} post tag: ${postTag} count: ${count}"

  if ("$postTag" -ne "") {
    $version = "${version}-${postTag}"
  }

  if ("$count" -ne "") {
    # For consistancy with previous releases we pad the counter to only 4 places
    $padded = $count.Trim().PadLeft(4, "0");
    Write-Debug "count '$count', padded '${padded}'"

    $version = "${version}${padded}"
  }
}
else {

  Write-Debug "Untagged"
  $lastTag = (git tag --list  --sort=-taggerdate) | Out-String
  $list = $lastTag.Split("`n")
  foreach ($tag in $list) {

    Write-Debug "Testing ${tag}"
    $tag = $tag.Trim();
    if ($tag -match $tagRegex) {
      Write-Debug "Matched ${tag}"
      $version = $matches[1];
      break;
    }
  }

  if ("$version" -eq "") {
    $version = $fallbackVersion
    Write-Debug  "Failed to discover base version Fallback to '${version}'"
  }
  else {

    Write-Debug  "Discovered base version from tags '${version}'"
  }

  # Create a build number based on the current datetime.
  $buildNumber = ""

  if ( "$env:GITHUB_SHA" -ne '') {
    $buildNumber = ToBuildNumber (git show -s --format=%ci $env:GITHUB_SHA)
  }
  elseif ( "$(git diff --stat)" -eq '') {
    $buildNumber = ToBuildNumber (git show -s --format=%ci HEAD)
  }
  else {
    $buildNumber = ToBuildNumber
  }

  $buildNumber = "$buildNumber".Trim().PadLeft(12, "0");

  Write-Debug "Building a branch commit"

  # This is a general branch commit
  $branch = ((git rev-parse --abbrev-ref HEAD) | Out-String).Trim()

  if ("$branch" -eq "") {
    $branch = "unknown"
  }

  $branch = $branch.Replace("/", "-").ToLower()

  if ($branch.ToLower() -eq "master" -or $branch.ToLower() -eq "head") {
    $branch = "dev"
  }

  $version = "${version}-${branch}${buildNumber}";
}

Write-Host "Building version '${version}'"

if ($targetFramework -ne 'ALL') {
  $targetFramework = "-f $targetFramework"
}

dotnet restore $targetFramework /p:packageversion=$version /p:DisableImplicitNuGetFallbackFolder=true

$repositoryUrl = ""

if ("$env:GITHUB_REPOSITORY" -ne "") {
  $repositoryUrl = "https://github.com/$env:GITHUB_REPOSITORY"
}

Write-Host "Building projects"

dotnet build -c Release $targetFramework /p:packageversion=$version /p:RepositoryUrl=$repositoryUrl

if ($LASTEXITCODE ) { Exit $LASTEXITCODE }

# Write-Host "Packaging projects"

# dotnet pack -c Release --output "$PSScriptRoot/artifacts" --no-build  /p:packageversion=$version /p:skipFullFramework=$skipFullFramework /p:RepositoryUrl=$repositoryUrl
# if ($LASTEXITCODE ) { Exit $LASTEXITCODE }
