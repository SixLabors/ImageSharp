FROM bpopow/dotnetsdkarm:v2
MAINTAINER Brian Popow <popow@gmx.de>

COPY . /app/
WORKDIR /app/tests/ImageSharp.Tests/

RUN dotnet build /p:Platform=arm64 -c Release -f net5.0

RUN cp /app/Magick.Native-Q16-x64.dll.so .
RUN cp /app/Magick.NET-Q16-AnyCPU.dll .
RUN cp /app/Magick.Native-Q16-x64.dll .
RUN cp /app/Magick.NET.Core.dll .

CMD ["dotnet", "test", "/p:Platform=arm64", "--no-build", "-c", "Release", "-f", "net5.0"]