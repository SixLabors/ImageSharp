@echo Off

SET versionCommand=
if not "%GitVersion_NuGetVersion%" == "" (
    SET versionCommand=/p:packageversion=%GitVersion_NuGetVersion%
    @echo building with version set to '%GitVersion_NuGetVersion%'
)

dotnet restore %versionCommand%

ECHO Building projects
dotnet build -c Release %versionCommand%

if not "%errorlevel%"=="0" goto failure

if not "%CI%" == "True"  (
    ECHO NOT on CI server running tests
    dotnet test ./tests/ImageSharp.Tests/ImageSharp.Tests.csproj --no-build -c Release
)
if not "%errorlevel%"=="0" goto failure

ECHO Packaging projects
dotnet pack ./src/ImageSharp/ -c Release --output ../../artifacts --no-build  %versionCommand%
if not "%errorlevel%"=="0" goto failure

dotnet pack ./src/ImageSharp.Drawing/ -c Release --output ../../artifacts --no-build  %versionCommand%
if not "%errorlevel%"=="0" goto failure

:success
ECHO successfully built project
REM exit 0
goto end

:failure
ECHO failed to build.
REM exit -1
goto end

:end