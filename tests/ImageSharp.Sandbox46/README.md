## Purpose
This project aims to workaround certain Visual Studio based .NET Core tooling issues of the time of it's creation (January 2017):
- .NET Core Performance profiling is not possible neither with Visual Studio nor with JetBrains profilers
- JetBrains Unit Test explorer does not work with .NET Core projects

## How does it work
- By referencing .NET 4.5 dll-s created by net45 target's of ImageSharp projects. NOTE: These are not project references!
- By including test classes (and utility classes) of the `ImageSharp.Tests` project using MSBUILD `<Link>`
- Compiling `ImageSharp.Sandbox46` should trigger the compilation of ImageSharp subprojects using a manually defined solution dependencies

## How to profile unit tests

