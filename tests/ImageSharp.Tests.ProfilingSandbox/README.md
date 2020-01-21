## Purpose
This project aims to workaround certain .NET Core tooling issues in Visual Studio based developer workflow at the time of it's creation (January 2017):
- .NET Core Performance profiling is not possible neither with Visual Studio nor with JetBrains profilers
- ~~JetBrains Unit Test explorer does not work with .NET Core projects~~

## How does it work?
- By referencing .NET 4.5 dll-s created by net45 target's of ImageSharp projects. NOTE: These are not project references!
- By including test classes (and utility classes) of the `ImageSharp.Tests` project using MSBUILD `<Link>`
- Compiling `ImageSharp.Sandbox46` should trigger the compilation of ImageSharp subprojects using a manually defined solution dependencies

## How to profile unit tests

#### 1. With Visual Studio 2015 Test Runner
- **Do not** build `ImageSharp.Tests`
- Build `ImageSharp.Sandbox46`
- Use the [context menu in Test Explorer](https://adamprescott.net/2012/12/12/performance-profiling-for-unit-tests/)

NOTE: 
There was no *Profile test* option in my VS Professional. Maybe things were messed by VS2017 RC installation. [This post suggests](http://stackoverflow.com/questions/32034375/profiling-tests-in-visual-studio-community-2015) it's necessary to own Premium or Ultimate edition of Visual Studio to profile tests.

#### 2. With JetBrains ReSharper Ultimate
- The `Sandbox46` project is no longer needed here. The classic `ImageSharp.Tests` project can be discovered by Unit Test Explorer.
- You can use [context menus](https://www.jetbrains.com/resharper/features/unit_testing.html) from your test class, or from unit Test Exporer/Unit Test Sessions windows.
![Context Menu](https://www.jetbrains.com/resharper/features/screenshots/100/unit_testing_profiling.png) 