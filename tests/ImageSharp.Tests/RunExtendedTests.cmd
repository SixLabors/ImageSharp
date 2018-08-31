dotnet build -c Release
dotnet xunit -nobuild -c Release -f net462
dotnet xunit -nobuild -c Release -f net462 -x86
dotnet xunit -nobuild -c Release -f net47
dotnet xunit -nobuild -c Release -f net47 -x86
dotnet xunit -nobuild -c Release -f net471
dotnet xunit -nobuild -c Release -f net471 -x86
