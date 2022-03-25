FROM mcr.microsoft.com/dotnet/sdk:5.0.406-buster-slim-arm64v8
MAINTAINER Brian Popow <popow@gmx.de>

RUN apt-get update
RUN apt-get -y install libgdiplus libgif-dev autoconf libtool automake build-essential gettext libglib2.0-dev libcairo2-dev libtiff-dev libexif-dev

COPY . /app/
WORKDIR /app/tests/ImageSharp.Tests/

RUN dotnet build /p:Platform=arm64 -c Release -f net5.0

CMD ["dotnet", "test", "/p:Platform=arm64", "--no-build", "-c", "Release", "-f", "net5.0"]