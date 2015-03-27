@echo off

powershell "Import-Module %~dp0\psake.psm1 ; Invoke-Psake %~dp0\build.ps1 Run-Coverage ; exit $LASTEXITCODE"

pause