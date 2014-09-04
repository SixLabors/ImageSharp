@ECHO OFF

SET version=2.0.1.0
SET webversion=4.0.0.0
SET webconfigversion=2.0.0.0
SET webppluginversion=1.0.1.0

ECHO Building ImageProcessor %version%, ImageProcessor.Web %webversion%, ImageProcessor.Web.Config %webconfigversion%, and ImageProcessor.Plugins.WebP %webppluginversion%

ECHO Removing _BuildOutput directory so everything is nice and clean
RD _BuildOutput /q /s

%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.ImageProcessor.proj" /p:BUILD_RELEASE=%version% /p:BUILD_COMMENT=%comment%
%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.ImageProcessor.Web.proj" /p:BUILD_RELEASE=%webversion% /p:BUILD_COMMENT=%comment%
%windir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe "Build.ImageProcessor.Plugins.WebP.proj" /p:BUILD_RELEASE=%webppluginversion% /p:BUILD_COMMENT=%comment%

ECHO Packing the NuGet release files
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.nuspec -Version %version%
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.Web.nuspec -Version %webversion%
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.Web.Config.nuspec -Version %webconfigversion%
..\src\.nuget\NuGet.exe pack NuSpecs\ImageProcessor.Plugins.WebP.nuspec -Version %webppluginversion%
PAUSE

IF ERRORLEVEL 1 GOTO :showerror

GOTO :EOF

:showerror
PAUSE
