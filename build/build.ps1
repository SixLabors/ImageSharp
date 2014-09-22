Properties {
	# Input and output paths
	$BUILD_PATH = Resolve-Path "."
	$SRC_PATH = Resolve-Path "..\src"
	$PLUGINS_PATH = Join-Path $SRC_PATH "Plugins\ImageProcessor"
	$NUSPECS_PATH = Join-Path $BUILD_PATH "NuSpecs"
	$BIN_PATH = Join-Path $BUILD_PATH "_BuildOutput"
	$NUGET_OUTPUT = Join-Path $BIN_PATH "NuGets"
	$TEST_RESULTS = Join-Path $BUILD_PATH "TestResults"
	
	# External binaries paths
	$NUGET_EXE = Join-Path $SRC_PATH ".nuget\NuGet.exe"
	$NUNIT_EXE = Join-Path $SRC_PATH "packages\NUnit.Runners.2.6.3\tools\nunit-console.exe"
	$OPENCOVER_EXE = Join-Path $SRC_PATH "packages\OpenCover.4.5.3207\OpenCover.Console.exe"
	$REPORTGEN_EXE = Join-Path $SRC_PATH "packages\ReportGenerator.1.9.1.0\ReportGenerator.exe"
	$NUNITREPORT_EXE = Join-Path $BUILD_PATH "tools\NUnitHTMLReportGenerator.exe"
	
	# list of projects
	[xml]$PROJECTS = Get-Content ".\build.xml"
	
	$TestProjects = @(
		"ImageProcessor.UnitTests",
		"ImageProcessor.Web.UnitTests"
	)
}

Framework "4.0x86"
FormatTaskName "-------- {0} --------"

task default -depends Cleanup-Binaries, Build-Solution, Run-Tests, Generate-Package

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

	# build the projects
	$PROJECTS.projects.project | % {
		if ($_.projfile -eq $null -or $_.projfile -eq "") {
			return # breaks out of ForEach-Object loop
		}
		
		$projectPath = Resolve-Path $_.folder
		Write-Host "Building project $($_.name) at version $($_.version)"

		# it would be possible to update more infos from the xml (description etc), so as to have all infos in one place
		Update-AssemblyInfo -file (Join-Path $projectPath "Properties\AssemblyInfo.cs") -version $_.version

		# using the invoke-expression on a string solves a few character escape issues
		$buildCommand = "msbuild $(Join-Path $projectPath $_.projfile) /t:Build /p:Warnings=true /p:Configuration=Release /p:PipelineDependsOnBuild=False /p:OutDir=$(Join-Path $BIN_PATH $($_.output)) /clp:WarningsOnly /clp:ErrorsOnly /clp:Summary /clp:PerformanceSummary /v:Normal /nologo"
		Exec {
			Invoke-Expression $buildCommand
		}
	}
}

# builds the test projects
task Build-Tests -depends Cleanup-Binaries {
	Write-Host "Building the unit test projects"
	
	if (-not (Test-Path $TEST_RESULTS)) {
		mkdir $TEST_RESULTS | Out-Null
	}
	
	# make sure the runner exes are restored
	& $NUGET_EXE restore (Join-Path $SRC_PATH "ImageProcessor.sln")
	
	# build the test projects
	$TestProjects | % {
		Write-Host "Building project $_"
		Exec {
			msbuild (Join-Path $SRC_PATH "$_\$_.csproj") /t:Build /p:Configuration=Release /p:Platform="AnyCPU" /p:Warnings=true /clp:WarningsOnly /clp:ErrorsOnly /v:Normal /nologo
		}
	}
	
}

# runs the unit tests
task Run-Tests -depends Build-Tests {
	Write-Host "Running unit tests"
	$TestProjects | % {
		$TestDllFolder = Join-Path $SRC_PATH "$_\bin\Release"
		$TestDdlPath = Join-Path $TestDllFolder "$_.dll"
		$TestOutputPath = Join-Path $TEST_RESULTS "$($_)_Unit.xml"
		
		Write-Host "Running unit tests on project $_"
		& $NUNIT_EXE $TestDdlPath /result:$TestOutputPath /noshadow /nologo
		
		$ReportPath = (Join-Path $TEST_RESULTS "Tests")
		if (-not (Test-Path $ReportPath)) {
			mkdir $ReportPath | Out-Null
		}
		
		Write-Host "Transforming tests results file to HTML"
		& $NUNITREPORT_EXE $TestOutputPath (Join-Path $ReportPath "$_.html")
	}
}

# runs the code coverage (separate from the unit test because it takes so much longer)
task Run-Coverage -depends Build-Tests {
	Write-Host "Running code coverage over unit tests"
	$TestProjects | % {
		$TestDllFolder = Join-Path $SRC_PATH "$_\bin\Release"
		$TestDdlPath = Join-Path $TestDllFolder "$_.dll"
		$CoverageOutputPath = Join-Path $TEST_RESULTS "$($_)_Coverage.xml"
		
		Write-Host "Running code coverage on project $_"
		& $OPENCOVER_EXE -register:user -target:$NUNIT_EXE -targetargs:"$TestDdlPath /noshadow /nologo" -targetdir:$TestDllFolder -output:$CoverageOutputPath
		
		Write-Host "Transforming coverage results file to HTML"
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
	$PROJECTS.projects.project | % {
		$nuspec_local_path = (Join-Path $NUSPECS_PATH $_.nuspec)
		Write-Host "Building Nuget package from $nuspec_local_path"
		
		# change the version values
		[xml]$nuspec_contents = Get-Content $nuspec_local_path
		$nuspec_contents.package.metadata.version = $_.version
		$nuspec_contents.Save($nuspec_local_path)
		
		# pack the nuget
		& $NUGET_EXE Pack $nuspec_local_path -OutputDirectory $NUGET_OUTPUT
	}
}

# updates the AssemblyInfo file with the specified version
# http://www.luisrocha.net/2009/11/setting-assembly-version-with-windows.html
function Update-AssemblyInfo ([string]$file, [string] $version) {
    $assemblyVersionPattern = 'AssemblyVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
    $fileVersionPattern = 'AssemblyFileVersion\("[0-9]+(\.([0-9]+|\*)){1,3}"\)'
    $assemblyVersion = 'AssemblyVersion("' + $version + '")';
    $fileVersion = 'AssemblyFileVersion("' + $version + '")';

    (Get-Content $file) | ForEach-Object {
        % {$_ -replace $assemblyVersionPattern, $assemblyVersion } |
        % {$_ -replace $fileVersionPattern, $fileVersion }
    } | Set-Content $file
}