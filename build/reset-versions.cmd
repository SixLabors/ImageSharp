@echo Off

set buildRoot="%cd%"
cd %~dp0

dotnet run -- reset

cd %buildRoot%