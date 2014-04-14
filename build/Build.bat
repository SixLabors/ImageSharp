@ECHO OFF
SET version=1.8.8.0
SET webversion=3.2.2.0
SET webconfigversion=1.0.0.0

ECHO Building ImageProcessor %version%, ImageProcess.Web %webversion% and ImageProcess.Web.Config %webconfigversion%

ECHO Installing the Microsoft.Bcl.Build package before anything else, otherwise you'd have to run build.cmd twice
SET nuGetFolder=%CD%\..\src\packages\
..\src\.nuget\NuGet.exe install ..\src\ImageProcessor.Web\NET45\packages.config -OutputDirectory %nuGetFolder%

ECHO Removing _BuildOutput directory so everything is nice and clean
RD _BuildOutput /q /s

%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.ImageProcessor.proj" /p:BUILD_RELEASE=%version% /p:BUILD_COMMENT=%comment%
%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.ImageProcessor.Web.proj" /p:BUILD_RELEASE=%webversion% /p:BUILD_COMMENT=%comment%

ECHO Packing the NuGet release files
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.nuspec -Version %version%
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.Web.nuspec -Version %webversion%
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.Web.Config.nuspec -Version %webconfigversion%


IF ERRORLEVEL 1 GOTO :showerror

GOTO :EOF

:showerror
PAUSE
