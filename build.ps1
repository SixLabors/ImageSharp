
# lets calulat the correct version here
$fallbackVersion = "1.0.0";
$version = ''

$tagRegex = '^v?(\d+\.\d+\.\d+)(-([a-zA-Z]+)\.?(\d*))?$'

# we are running on the build server 
$isVersionTag = $env:APPVEYOR_REPO_TAG_NAME -match $tagRegex

 if($isVersionTag) {
     
    Write-Debug "Building commit tagged with a compatable version number"
    
    $version = $matches[1]
     $postTag = $matches[3]
     $count = $matches[4]
     Write-Debug "version number: ${version} post tag: ${postTag} count: ${count}"
     if("$postTag" -ne ""){
        $version = "${version}-${postTag}"
     }
     if("$count" -ne ""){
        # for consistancy with previous releases we pad the counter to only 4 places
        $padded = $count.Trim().Trim('0').PadLeft(4,"0");
        Write-Debug "count '$count', padded '${padded}'"

        $version = "${version}${padded}"
     }
 }
 else {
     
    Write-Debug "Untagged"
    $lastTag =  (git tag --list  --sort=-taggerdate)  | Out-String
    $list = $lastTag.Split("`n")
    foreach ($tag in $list) { 
        
    Write-Debug "testing ${tag}"
        $tag = $tag.Trim();
        if($tag -match $tagRegex){
            Write-Debug "matched ${tag}"
            $version = $matches[1];
            break;
        }
    }
    
    if("$version" -eq ""){ 
        $version = $fallbackVersion 
        Write-Debug  "Failed to discover base version Fallback to '${version}'"
    }else{
        
        Write-Debug  "Discovered base version from tags '${version}'"
    }

    $buildNumber = $env:APPVEYOR_BUILD_NUMBER

    # build number replacement is padded to 6 places
    $buildNumber = "$buildNumber".Trim().Trim('0').PadLeft(6,"0");
    if("$env:APPVEYOR_PULL_REQUEST_NUMBER" -ne ""){
        Write-Debug "building a PR"
        
        $prNumber = "$env:APPVEYOR_PULL_REQUEST_NUMBER".Trim().Trim('0').PadLeft(5,"0");
        # this is a PR
        $version = "${version}-PullRequest${prNumber}${buildNumber}";
    }else{
        Write-Debug "building a branch commit"

        # this is a general branch commit
        $branch = $env:APPVEYOR_REPO_BRANCH
        
        if("$branch" -eq ""){
            $branch = ((git rev-parse --abbrev-ref HEAD) | Out-String).Trim()
            
            if("$branch" -eq ""){
                $branch = "unknown"
            }
        }

        $branch = $branch.Replace("/","-").ToLower()

        if($branch.ToLower() -eq "master"){
            $branch = "dev"
        }
        
        $version = "${version}-${branch}${buildNumber}";
    }
 }

if("$env:APPVEYOR_API_URL" -ne ""){
    # update appveyor build number for this build
    Invoke-RestMethod -Method "PUT" `
                      -Uri "${env:APPVEYOR_API_URL}api/build" `
                      -Body "{version:'${version}'}" `
                      -ContentType "application/json"
}

Write-Host "Building version '${version}'"
dotnet restore /p:packageversion=$version /p:DisableImplicitNuGetFallbackFolder=true

Write-Host "Building projects"
dotnet build -c Release /p:packageversion=$version

if ($LASTEXITCODE ){ Exit $LASTEXITCODE }

#
# TODO: DO WE NEED TO RUN TESTS IMPLICITLY?
#
# if ( $env:CI -ne "True") {
#     cd ./tests/ImageSharp.Tests/
#     dotnet xunit -nobuild -c Release -f netcoreapp2.0 --fx-version 2.0.0
#     ./RunExtendedTests.cmd
#     cd ../..
# }
#

if ($LASTEXITCODE ){ Exit $LASTEXITCODE }

Write-Host "Packaging projects"
dotnet pack ./src/ImageSharp/ -c Release --output ../../artifacts --no-build  /p:packageversion=$version
if ($LASTEXITCODE ){ Exit $LASTEXITCODE }

dotnet pack ./src/ImageSharp.Drawing/ -c Release --output ../../artifacts --no-build  /p:packageversion=$version
if ($LASTEXITCODE ){ Exit $LASTEXITCODE }
