@echo Off

if not "%GitVersion_NuGetVersion%" == "" (
    dotnet restore /p:packageversion=%GitVersion_NuGetVersion%
)ELSE (
    dotnet restore
)

ECHO Building nuget packages
if not "%GitVersion_NuGetVersion%" == "" (
    dotnet build -c Release /p:packageversion=%GitVersion_NuGetVersion%
)ELSE (
    dotnet build -c Release
)
if not "%errorlevel%"=="0" goto failure

dotnet test ./tests/SixLabors.Core.Tests/SixLabors.Primitives.Tests.csproj


if not "%GitVersion_NuGetVersion%" == "" (
    dotnet pack ./src/SixLabors.Core/ -c Release --output ../../artifacts --no-build /p:packageversion=%GitVersion_NuGetVersion%
)ELSE (
    dotnet pack ./src/SixLabors.Core/ -c Release --output ../../artifacts --no-build
)

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