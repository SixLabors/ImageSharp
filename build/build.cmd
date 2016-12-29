@echo Off
set buildRoot="%cd%"

ECHO restoring packages
dotnet restore

ECHO Updating version numbers and generating build script
cd %~dp0
dotnet run -- update
cd %buildRoot%

ECHO Building package
call %~dp0build-inner.cmd

ECHO Reset version numbers
cd %~dp0
dotnet run -- reset
cd %buildRoot%