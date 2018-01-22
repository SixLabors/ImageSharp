@echo Off

PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& '.\build.ps1'"

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