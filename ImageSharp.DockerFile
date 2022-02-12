FROM mcr.microsoft.com/dotnet/sdk:5.0.405-focal-arm64v8
MAINTAINER Brian Popow <popow@gmx.de>

RUN apt-get update
RUN apt-get -y install libgdiplus libgif-dev autoconf libtool automake build-essential gettext libglib2.0-dev libcairo2-dev libtiff-dev libexif-dev

COPY . /app/
WORKDIR /app/
RUN dotnet restore
RUN dotnet build
CMD ["dotnet", "test", "tests/ImageSharp.Tests/ImageSharp.Tests.csproj", "-c", "Release", "-f", "net5.0"]