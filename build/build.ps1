Properties {
	$version = "2.0.1.0"
	$webversion = "4.0.0.0"
	$webconfigversion = "2.0.0.0"
	$webppluginversion = "1.0.1.0"
	$cairpluginversion = "1.0.0.0"
	
	# build paths to various files
	$PROJ_PATH = Resolve-Path "."
	$SRC_PATH = Resolve-Path "..\src"
	$BIN_PATH = Join-Path $PROJ_PATH "_BuildOutput"
	$NUGET_EXE = Join-Path $SRC_PATH ".nuget\NuGet.exe"
	$NUSPECS_PATH = Join-Path $PROJ_PATH "NuSpecs"
	$NUGET_OUTPUT = Join-Path $BIN_PATH "NuGets"
	
	# nunit runner binaries
	$NUNIT_EXE = Join-Path $SRC_PATH "packages\NUnit.Runners.2.6.3\tools\nunit-console.exe"
}

Framework "4.0x86"
FormatTaskName "-------- {0} --------"

task default -depends Cleanup-Binaries, Build-Solution, Run-Tests, Generate-Package

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
	$projects = @(
		"Build.ImageProcessor.proj",
		"Build.ImageProcessor.Web.proj",
		"Build.ImageProcessor.Plugins.WebP.proj",
		"Build.ImageProcessor.Plugins.Cair.proj"
	)
	
	$projects | % {
		Write-Host "Building project $_"
		Exec {
			msbuild (Join-Path $PROJ_PATH $_) /p:BUILD_RELEASE="$version"
		}
	}
}

# runs the unit tests
task Run-Tests {
	Write-Host "Building the unit test projects"
	
	$projects = @(
		"ImageProcessor.UnitTests",
		"ImageProcessor.Web.UnitTests"
	)
	
	$projects | % {
		Write-Host "Building project $_"
		Exec {
			msbuild (Join-Path $SRC_PATH "$_\$_.csproj") /t:Build /p:Configuration=Release /p:Platform="AnyCPU" /p:Warnings=true /v:Normal /nologo /clp:WarningsOnly`;ErrorsOnly`;Summary`;PerformanceSummary
		}
	}
	
	Write-Host "Running unit tests"
	$projects | % {
		Write-Host "Running tests on project $_"
		& $NUNIT_EXE (Join-Path $SRC_PATH "$_\bin\Release\$_.dll")
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
	$nuspecs = @{
		"ImageProcessor.nuspec" = $version ;
		"ImageProcessor.Web.nuspec" = $webversion ;
		"ImageProcessor.Web.Config.nuspec" = $webconfigversion ;
		"ImageProcessor.Plugins.WebP.nuspec" = $webppluginversion ;
		"ImageProcessor.Plugins.Cair.nuspec" = $cairpluginversion
	}
	
	$nuspecs.GetEnumerator() | % {
		$nuspec_local_path = (Join-Path $NUSPECS_PATH $_.Key)
		Write-Host "Building Nuget package from $nuspec_local_path"
		
		# change the version values
		[xml]$nuspec_contents = Get-Content $nuspec_local_path
		$nuspec_contents.package.metadata.version = $_.Value
		$nuspec_contents.Save($nuspec_local_path)
		
		# pack the nuget
		& $NUGET_EXE Pack $nuspec_local_path -OutputDirectory $NUGET_OUTPUT
	}
}