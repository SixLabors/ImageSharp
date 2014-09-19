Properties {
	$version = "2.0.1.0"
	$webversion = "4.0.0.0"
	$webconfigversion = "2.0.0.0"
	$webppluginversion = "1.0.1.0"
	$cairpluginversion = "1.0.0.0"
	
	$PROJ_PATH = "."
	$BIN_PATH = (Join-Path $PROJ_PATH "_BuildOutput")
	$NUGET_EXE = "..\src\.nuget\NuGet.exe"
	$NUSPECS_PATH = (Join-Path $PROJ_PATH "NuSpecs")
	$NUGET_OUTPUT = (Join-Path $BIN_PATH "NuGets")
	# TODO: add opencover and nunit runner binaries
}

Framework "4.0x86"
FormatTaskName "-------- {0} --------"

task default -depends Cleanup-Binaries, Build-Solution, Generate-Package

# cleans up the binaries output folder
task Cleanup-Binaries {
	Write-Host "Removing $BIN_PATH directory so everything is nice and clean"
	if (Test-Path $BIN_PATH) {
		Remove-Item $BIN_PATH -Force -Recurse
	}
}

# builds the solutions
task Build-Solution -depends Cleanup-Binaries {
	Write-Host "Building projects"
	Exec {
		msbuild (Join-Path $PROJ_PATH "Build.ImageProcessor.proj") /p:BUILD_RELEASE="$version"
		msbuild (Join-Path $PROJ_PATH "Build.ImageProcessor.Web.proj") /p:BUILD_RELEASE="$version"
		msbuild (Join-Path $PROJ_PATH "Build.ImageProcessor.Plugins.WebP.proj") /p:BUILD_RELEASE="$version"
		msbuild (Join-Path $PROJ_PATH "Build.ImageProcessor.Plugins.Cair.proj") /p:BUILD_RELEASE="$version"
	}
}

# generates a Nuget package
task Generate-Package -depends Build-Solution {
	Write-Host "Generating Nuget packages for each project"
	
	# Nuget doesn't create the output dir automatically...
	if (-not (Test-Path $NUGET_OUTPUT)) {
		mkdir $NUGET_OUTPUT | Out-Null
	}
	
	# Package the nuget
	& $NUGET_EXE Pack (Join-Path $NUSPECS_PATH "ImageProcessor.nuspec") -OutputDirectory $NUGET_OUTPUT
	& $NUGET_EXE Pack (Join-Path $NUSPECS_PATH "ImageProcessor.Web.nuspec") -OutputDirectory $NUGET_OUTPUT
	& $NUGET_EXE Pack (Join-Path $NUSPECS_PATH "ImageProcessor.Web.Config.nuspec") -OutputDirectory $NUGET_OUTPUT
	& $NUGET_EXE Pack (Join-Path $NUSPECS_PATH "ImageProcessor.Plugins.WebP.nuspec") -OutputDirectory $NUGET_OUTPUT
	& $NUGET_EXE Pack (Join-Path $NUSPECS_PATH "ImageProcessor.Plugins.Cair.nuspec") -OutputDirectory $NUGET_OUTPUT
}