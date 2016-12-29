

@echo Off
REM include project configs
call %~dp0\config.cmd

set buildRoot="%cd%"

ECHO Reseting build version numbers
for %%s in (%projects%) do ( 
    cd %%s
    ECHO %GitVersion_NuGetVersion%
    dotnet version "1.0.0-*"
    cd %buildRoot%
)

:success
REM exit 0
goto end

:failure
REM exit -1
goto end

:end
