FROM bpopow/dotnetsdkarm:v4
MAINTAINER Brian Popow <popow@gmx.de>

COPY . /app/
WORKDIR /app/tests/ImageSharp.Tests/

RUN dotnet build /p:Platform=arm64 -c Release -f net5.0

RUN cp /app/artifacts/Magick.Native-Q16-x64.dll.so /app/artifacts/bin/tests/ImageSharp.Tests/arm64/Release/net5.0/Magick.Native-Q16-x64.dll.so

CMD ["dotnet", "test", "/p:Platform=arm64", "--no-build", "-c", "Release", "-f", "net5.0"]