.\packages\OpenCover.4.6.519\tools\OpenCover.Console.exe -register:user "-filter:+[Bom]* -[*Test]*" "-target:.\packages\NUnit.Runners.2.6.4\tools\nunit-console-x86.exe" "-targetargs:/noshadow .\BomTest\bin\Debug\BomTest.dll"

.\packages\ReportGenerator.2.1.8.0\tools\ReportGenerator.exe "-reports:results.xml" "-targetdir:.\coverage"

pause