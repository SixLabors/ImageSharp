@echo off

cd tests\CodeCoverage

nuget restore packages.config -PackagesDirectory .

cd ..
cd ..

dotnet restore SixLabors.Core.sln
dotnet build SixLabors.Core.sln --no-incremental -c release /p:codecov=true

tests\CodeCoverage\OpenCover.4.6.519\tools\OpenCover.Console.exe -target:"dotnet.exe" -targetargs:"test tests\SixLabors.Core.Tests\SixLabors.Core.Tests.csproj --no-build -c release" -searchdirs:"tests\SixLabors.Core.Tests\bin\Release\netcoreapp1.1" -register:user -output:.\SixLabors.Core.Coverage.xml -hideskipped:All -returntargetcode -oldStyle -filter:"+[SixLabors.*]*" 

if %errorlevel% neq 0 exit /b %errorlevel%

SET PATH=C:\\Python34;C:\\Python34\\Scripts;%PATH%
pip install codecov
codecov -f "SixLabors.Core.Coverage.xml"