

@echo Off
ECHO Starting build

call build\config.cmd

ECHO Restoring packages
for %%s in (%projects%) do dotnet restore %%s

call build\update-versions.cmd

set buildRoot="%~dp0"
SET cli=dotnet pack --configuration Release --output "artifacts\bin\ImageSharp"
where gitversion
if not "%errorlevel%"=="0" (
    REM gitversion was not availible lets make a local build 
    SET cli=%cli% --version-suffix "local-build"
)

ECHO Building packages
for %%s in (%projects%) do %cli% %%s

REM reset local version numbers 
call build\reset-versions.cmd

:success
ECHO successfully built project
REM exit 0
goto end

:failure
ECHO failed to build.
REM exit -1
goto end

:end
