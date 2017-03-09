@echo off

cd tests\CodeCoverage

nuget restore packages.config -PackagesDirectory .

cd ..
cd ..

dotnet restore  ImageSharp.sln
dotnet build  ImageSharp.sln --no-incremental -c release /p:codecov=true
rem The -threshold options prevents this taking ages...
tests\CodeCoverage\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"dotnet.exe" -targetargs:"test tests\ImageSharp.Tests\ImageSharp.Tests.csproj --no-build -c release" -searchdirs:"tests\ImageSharp\bin\Release\netcoreapp1.1" -register:user -output:.\ImageSharp.Coverage.xml -hideskipped:All -returntargetcode -oldStyle -filter:"+[ImageSharp*]*" 

if %errorlevel% neq 0 exit /b %errorlevel%

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "ImageSharp.Coverage.xml"