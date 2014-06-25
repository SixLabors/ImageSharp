$solutionDir = [System.IO.Path]::GetDirectoryName($dte.Solution.FullName) + "\"
$path = $installPath.Replace($solutionDir, "`$(SolutionDir)")

$NativeAssembliesDir = Join-Path $path "lib"
$x86 = $(Join-Path $NativeAssembliesDir "x86\*.*")
$x64 = $(Join-Path $NativeAssembliesDir "x64\*.*")

$ImageProcessorPostBuildCmd = "
if not exist `"`$(TargetDir)x86`" md `"`$(TargetDir)x86`"
xcopy /s /y `"$x86`" `"`$(TargetDir)x86`"
if not exist `"`$(TargetDir)amd64`" md `"`$(TargetDir)x64`"
xcopy /s /y `"$x64`" `"`$(TargetDir)x64`""