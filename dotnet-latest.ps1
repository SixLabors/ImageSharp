# Set up everything for using the dotnet cli. This should mean we do not have to wait for Appveyor images to be updated.

# Clean and recreate the folder in which all output packages should be placed
$ArtifactsPath = "artifacts"

if (Test-Path $ArtifactsPath) { 
	Remove-Item -Path $ArtifactsPath -Recurse -Force -ErrorAction Ignore 
}

New-Item $ArtifactsPath -ItemType Directory -ErrorAction Ignore | Out-Null

Write-Host "Created artifacts folder '$ArtifactsPath'"
$installRequired = $TRUE

# Install the latest dotnet cli   
if (Get-Command "dotnet.exe" -ErrorAction SilentlyContinue) {
    Write-Host "dotnet SDK already installed"

	$version = dotnet --version 2>&1
    if($version -ne "1.0.0-rc3-004530"){
        Write-Host "$version installed but require 1.0.0-rc3-004530"
        $installRequired = $TRUE
    }else{
        Write-Host "$version already installed"
        $installRequired = $FALSE
    } 
} 

if($installRequired -eq $TRUE)
{
    Write-Host "Installing dotnet SDK"
    
    $installScript = Join-Path $ArtifactsPath "dotnet-install.ps1"
    
	Write-Host $installScript

     Invoke-WebRequest "https://raw.githubusercontent.com/dotnet/cli/rel/1.0.0-rc3/scripts/obtain/dotnet-install.ps1" `
       -OutFile $installScript
        
     & $installScript
}
