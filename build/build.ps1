Properties {
	# call nuget.bat with these values as parameters
	$NugetApiKey = $null
	$NugetSource = $null
	
	# see appveyor.yml for usage
	$BuildNumber = $null
	$CoverallsRepoToken = $null
	$AppVeyor = $null
	
	# Input and output paths
	$BUILD_PATH = Resolve-Path "."
	$SRC_PATH = Join-Path $BUILD_PATH "..\src"
	$PLUGINS_PATH = Join-Path $SRC_PATH "Plugins\ImageProcessor"
	$NUSPECS_PATH = Join-Path $BUILD_PATH "NuSpecs"
	$BIN_PATH = Join-Path $BUILD_PATH "_BuildOutput"
	$NUGET_OUTPUT = Join-Path $BIN_PATH "NuGets"
	$TEST_RESULTS = Join-Path $BUILD_PATH "TestResults"

	# API documentation
	$API_BIN_PATH = Join-Path $BIN_PATH "ImageProcessor\lib\net45\ImageProcessor.dll" # from which DLL Docu builds its help output
	$API_DOC_PATH = Join-Path $BIN_PATH "Help\docu"
	
	# External binaries paths
	$NUGET_EXE = Join-Path $SRC_PATH ".nuget\NuGet.exe"
	$NUNIT_EXE = Join-Path $SRC_PATH "packages\NUnit.Runners.2.6.3\tools\nunit-console.exe"
	$COVERALLS_EXE = Join-Path $SRC_PATH "packages\coveralls.io.1.3.2\tools\coveralls.net.exe"
	$OPENCOVER_EXE = Join-Path $SRC_PATH "packages\OpenCover.4.5.3809-rc94\OpenCover.Console.exe"
	$REPORTGEN_EXE = Join-Path $SRC_PATH "packages\ReportGenerator.1.9.1.0\ReportGenerator.exe"
	$NUNITREPORT_EXE = Join-Path $BUILD_PATH "tools\NUnitHTMLReportGenerator.exe"
	
	# list of projects
	$PROJECTS_PATH = (Join-Path $BUILD_PATH "build.xml")
	[xml]$PROJECTS = Get-Content $PROJECTS_PATH
	
	$TestProjects = @(
		"ImageProcessor.UnitTests",
		"ImageProcessor.Web.UnitTests"
	)
}

Framework "4.0x86"
FormatTaskName "-------- {0} --------"

task default -depends Cleanup-Binaries, Set-VersionNumber, Build-Solution, Run-Tests, Run-Coverage, Generate-Nuget

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

# sets the version number from the build number in the build.xml file
task Set-VersionNumber {
	if ($BuildNumber -eq $null -or $BuildNumber -eq "") {
		return
	}
	
	$PROJECTS.projects.project | % {
		if ($_.version -match "([\d+\.]*)[\d+|\*]") { # get numbers of current version except last one
			$_.version = "$($Matches[1])$BuildNumber"
		}
	}
	$PROJECTS.Save($PROJECTS_PATH)
}

# builds the solutions
task Build-Solution -depends Cleanup-Binaries, Set-VersionNumber {
	Write-Host "Building projects"

	# build the projects
	# regular "$xmlobject.node | % { $_ }" don't work when they're nested: http://fredmorrison.wordpress.com/2013/03/19/reading-xml-with-powershell-why-most-examples-you-see-are-wrong/
	[System.Xml.XmlElement] $root = $PROJECTS.get_DocumentElement()
	[System.Xml.XmlElement] $project = $null
	foreach($project in $root.ChildNodes) {
		if ($project.projfile -eq $null -or $project.projfile -eq "") {
			continue # goes to next item
		}

		$projectPath = Resolve-Path $project.folder
		Write-Host "Building project $($project.name) at version $($project.version)"

		# it would be possible to update more infos from the xml (description etc), so as to have all infos in one place
		Update-AssemblyInfo -file (Join-Path $projectPath "Properties\AssemblyInfo.cs") -version $project.version

		[System.Xml.XmlElement] $output = $null
		foreach($output in $project.outputs.ChildNodes) {
			# using invoke-expression solves a few character escape issues
			$buildCommand = "msbuild $(Join-Path $projectPath $project.projfile) /t:Build /p:Warnings=true /p:Configuration=Release /p:PipelineDependsOnBuild=False /p:OutDir=$(Join-Path $BIN_PATH $output.folder) $($output.additionalParameters) /clp:WarningsOnly /clp:ErrorsOnly /clp:Summary /clp:PerformanceSummary /v:Normal /nologo"
			Write-Host $buildCommand -ForegroundColor DarkGreen
			Exec {
				Invoke-Expression $buildCommand
			}
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
		
		Write-Host "AppVeyor $AppVeyor"

	    $appVeyor = ""
	    if ($AppVeyor -ne $null -and $AppVeyor -ne "") {
	        $appVeyor = " -appveyor"
	    }

		Write-Host "Running code coverage on project $_"
		$coverageFilter = "-filter:+[*]* -[FluentAssertions*]* -[ImageProcessor]*Common.Exceptions -[ImageProcessor.UnitTests]* -[ImageProcessor.Web.UnitTests]*"
		& $OPENCOVER_EXE -threshold:1 -oldstyle -register:user -target:$NUNIT_EXE -targetargs:"$TestDdlPath /noshadow /nologo" $appVeyor -targetdir:$TestDllFolder -output:$CoverageOutputPath $coverageFilter
		
		Write-Host "Transforming coverage results file to HTML"
		& $REPORTGEN_EXE -verbosity:Info -reports:$CoverageOutputPath -targetdir:(Join-Path $TEST_RESULTS "Coverage\$_")

        Write-Host "CoverallsRepoToken $CoverallsRepoToken"

	    if ($CoverallsRepoToken -ne $null -and $CoverallsRepoToken -ne "") {
			Write-Host "Uploading coverage report to Coveralls.io"
	        Exec { . $COVERALLS_EXE --opencover $CoverageOutputPath }
	    }
	}
}

# generates the API documentation. Disabled for now.
task Generate-APIDoc -depends Build-Solution {
	Write-Host "Generating API docs"

	& .\tools\docu\docu.exe $API_BIN_PATH --output=$API_DOC_PATH

	& .\tools\doxygen\doxygen.exe .\Doxyfile
}

# generates a Nuget package
task Generate-Nuget -depends Set-VersionNumber, Build-Solution {
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
		
		if ((-not (Test-Path $nuspec_local_path)) -or (-not (Test-Path $NUGET_OUTPUT))) {
			throw New-Object [System.IO.FileNotFoundException] "The file $nuspec_local_path or $NUGET_OUTPUT could not be found"
		}
		
		# pack the nuget
		& $NUGET_EXE Pack $nuspec_local_path -OutputDirectory $NUGET_OUTPUT
	}
}

# publishes the nuget on a feed
task Publish-Nuget {
	if ($NugetApiKey -eq $null -or $NugetApiKey -eq "") {
		throw New-Object [System.ArgumentException] "You must provide an API key as parameter: 'Invoke-psake Publish-Nuget -properties @{`"NugetApiKey`"=`"YOURAPIKEY`"}' ; or add a APIKEY environment variable to AppVeyor"
	}
	
	Get-ChildItem $NUGET_OUTPUT -Filter "*.nugpkg" | % {
		if ($NugetSource -eq $null -or $NugetSource -eq "") {
			& $NUGET_EXE push $_ -ApiKey $apikey -Source $NugetSource
		} else {
			& $NUGET_EXE push $_ -ApiKey $apikey
		}
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