@echo off

cd tests\CodeCoverage

nuget restore packages.config -PackagesDirectory .

cd ..\ImageSharp.Tests

dotnet restore

rem The -threshold options prevents this taking ages...
..\CodeCoverage\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"C:\Program Files\dotnet\dotnet.exe" -targetargs:"test -c Release -f net451" -threshold:10 -register:user -filter:"+[ImageSharp*]*" -excludebyattribute:*.ExcludeFromCodeCoverage* -output:.\ImageSharp.Coverage.xml

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "ImageSharp.Coverage.xml"

cd ..
cd ..