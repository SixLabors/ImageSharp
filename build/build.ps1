Properties {
	$version = "2.0.1.0"
	$webversion = "4.0.0.0"
	$webconfigversion = "2.0.0.0"
	$webppluginversion = "1.0.1.0"
	$cairpluginversion = "1.0.0.0"
	
	# Input and output paths
	$PROJ_PATH = Resolve-Path "."
	$SRC_PATH = Resolve-Path "..\src"
	$NUSPECS_PATH = Join-Path $PROJ_PATH "NuSpecs"
	$BIN_PATH = Join-Path $PROJ_PATH "_BuildOutput"
	$NUGET_OUTPUT = Join-Path $BIN_PATH "NuGets"
	$TEST_RESULTS = Join-Path $PROJ_PATH "TestResults"
	
	# External binaries paths
	$NUGET_EXE = Join-Path $SRC_PATH ".nuget\NuGet.exe"
	$NUNIT_EXE = Join-Path $SRC_PATH "packages\NUnit.Runners.2.6.3\tools\nunit-console.exe"
	$OPENCOVER_EXE = Join-Path $SRC_PATH "packages\OpenCover.4.5.3207\OpenCover.Console.exe"
	$REPORTGEN_EXE = Join-Path $SRC_PATH "packages\ReportGenerator.1.9.1.0\ReportGenerator.exe"
}

Framework "4.0x86"
FormatTaskName "-------- {0} --------"

task default -depends Cleanup-Binaries, Build-Solution, Generate-Package

# cleans up the binaries output folder
task Cleanup-Binaries {
	Write-Host "Removing binaries and artifacts so everything is nice and clean"
	if (Test-Path $BIN_PATH) {
		Remove-Item $BIN_PATH -Force -Recurse
	}
	
	if (Test-Path $NUGET_OUTPUT) {
		Remove-Item $NUGET_OUTPUT -Force -Recurse
	}
	
	if (Test-Path $TEST_RESULTS) {
		Remove-Item $TEST_RESULTS -Force -Recurse
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
task Run-Tests -depends Cleanup-Binaries {
	Write-Host "Building the unit test projects"
	
	if (-not (Test-Path $TEST_RESULTS)) {
		mkdir $TEST_RESULTS | Out-Null
	}
	
	# make sure the runner exes are restored
	& $NUGET_EXE restore (Join-Path $SRC_PATH "ImageProcessor.sln")
	
	$projects = @(
		"ImageProcessor.UnitTests",
		"ImageProcessor.Web.UnitTests"
	)
	
	# build the test projects (they don't have specific build files like the main projects)
	$projects | % {
		Write-Host "Building project $_"
		Exec {
			msbuild (Join-Path $SRC_PATH "$_\$_.csproj") /t:Build /p:Configuration=Release /p:Platform="AnyCPU" /p:Warnings=true /v:Normal /nologo
		}
	}
	
	# run the Nunit test runner on the test DLLs
	Write-Host "Running code coverage over unit tests"
	$projects | % {
		$TestDllFolder = Join-Path $SRC_PATH "$_\bin\Release"
		$TestDdlPath = Join-Path $TestDllFolder "$_.dll"
		$TestOutputPath = Join-Path $TEST_RESULTS "$($_)_Unit.xml"
		$CoverageOutputPath = Join-Path $TEST_RESULTS "$($_)_Coverage.xml"
		
		Write-Host "Running code coverage on project $_"
		& $OPENCOVER_EXE -register:user -target:$NUNIT_EXE -targetargs:"$TestDdlPath /result:$TestOutputPath /noshadow /nologo" -targetdir:$TestDllFolder -output:$CoverageOutputPath
		
		Write-Host "Transforming coverage result file to HTML"
		& $REPORTGEN_EXE -reports:$TestOutputPath -targetdir:(Join-Path $TEST_RESULTS "Tests\$_")
		& $REPORTGEN_EXE -reports:$CoverageOutputPath -targetdir:(Join-Path $TEST_RESULTS "Coverage\$_")
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