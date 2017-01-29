@echo off

cd tests\CodeCoverage

nuget restore packages.config -PackagesDirectory .

cd ..\ImageSharp.Tests

dotnet restore

cd ..
cd ..

rem The -threshold options prevents this taking ages...
tests\CodeCoverage\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"dotnet.exe" -targetargs:"test tests\ImageSharp.Tests\ImageSharp.Tests.csproj -c Release -f net45" -threshold:10 -register:user -filter:"+[ImageSharp*]*" -excludebyattribute:*.ExcludeFromCodeCoverage* -hideskipped:All -returntargetcode -output:.\ImageSharp.Coverage.xml

if %errorlevel% neq 0 exit /b %errorlevel%

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "ImageSharp.Coverage.xml"