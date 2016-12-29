

@echo Off
REM include project configs
call %~dp0\config.cmd

REM gitversion not already been set in this build
if "%GitVersion_NuGetVersion%" == "" (
    rem can I call gitversion
    where gitversion
    if "%errorlevel%"=="0" (
        REM call gitversion and then recall this build script with the envArgs set
        ECHO calculating correct version number
        
        REM call this file from itself with the args set
       gitversion /output buildserver /exec "%~dp0\update-versions.cmd"
        
        REM we looped skip to the end
        goto end
    )
)

set buildRoot="%cd%"

ECHO Updating build version numbers
for %%s in (%projects%) do ( 
    cd %%s
    ECHO %GitVersion_NuGetVersion%
    dotnet version %GitVersion_NuGetVersion%
    cd %buildRoot%
)

:success
REM exit 0
goto end

:failure
REM exit -1
goto end

:end
