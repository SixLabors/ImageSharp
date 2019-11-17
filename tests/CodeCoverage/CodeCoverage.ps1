
if((Test-Path("$PSScriptRoot\OpenCover.4.6.519")) -eq $false){
    Invoke-WebRequest https://www.nuget.org/api/v2/package/OpenCover/4.7.922 -OutFile "$PSScriptRoot\opencover.zip"
    [IO.Compression.Zipfile]::ExtractToDirectory("$PSScriptRoot\opencover.zip","$PSScriptRoot\OpenCover.4.6.519")
}

dotnet clean ImageSharp.sln -c Release

& "$PSScriptRoot\OpenCover.4.6.519\tools\OpenCover.Console.exe" -target:"dotnet.exe" -targetargs:"test tests\ImageSharp.Tests\ImageSharp.Tests.csproj -c Release -f netcoreapp2.1 /p:skipFullFramework=true /p:codecov=true" -register:user -threshold:10 -oldStyle -safemode:off -output:.\ImageSharp.Coverage.xml -hideskipped:All -returntargetcode -filter:"+[SixLabors.ImageSharp*]*" 

if ($LASTEXITCODE ){ Exit $LASTEXITCODE }