<h1 align="center">

<img src="https://github.com/SixLabors/Branding/raw/master/icons/imagesharp/sixlabors.imagesharp.svg?sanitize=true" alt="SixLabors.ImageSharp" width="256"/>
<br/>
SixLabors.ImageSharp
</h1>


<div align="center">

[![Build Status](https://img.shields.io/github/workflow/status/SixLabors/ImageSharp/Build/master)](https://github.com/SixLabors/ImageSharp/actions)
[![Code coverage](https://codecov.io/gh/SixLabors/ImageSharp/branch/master/graph/badge.svg)](https://codecov.io/gh/SixLabors/ImageSharp)
[![License: AGPL v3](https://img.shields.io/badge/license-AGPL%20v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/ImageSharp/General?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=flat&logo=twitter)](https://twitter.com/intent/tweet?hashtags=imagesharp,dotnet,oss&text=ImageSharp.+A+new+cross-platform+2D+graphics+API+in+C%23&url=https%3a%2f%2fgithub.com%2fSixLabors%2fImageSharp&via=sixlabors)

</div>

### **ImageSharp** is a new, fully featured, fully managed, cross-platform, 2D graphics API. 

ImageSharp is a new, fully featured, fully managed, cross-platform, 2D graphics library. Designed to simplify image processing, ImageSharp brings you an incredibly powerful yet beautifully simple API.

ImageSharp is designed from the ground up to be flexible and extensible. The library provides API endpoints for common image processing operations and the building blocks to allow for the development of addtional operations.

Built against [.NET Standard 1.3](https://docs.microsoft.com/en-us/dotnet/standard/net-standard), ImageSharp can be used in device, cloud, and embedded/IoT scenarios.


## License
  
- ImageSharp is licensed under the [GNU Affero General Public License v3](https://www.gnu.org/licenses/agpl-3.0)  
- An alternative Commercial License can be purchased for Closed Source projects and applications.
Please visit https://sixlabors.com/pricing for details.
- Open Source projects who have taken a dependency on ImageSharp prior to adoption of the AGPL v3 license are permitted to use ImageSharp (including all future versions) under the previous [Apache 2.0 License](https://opensource.org/licenses/Apache-2.0).

## Documentation

- [Detailed documentation](https://sixlabors.github.io/docs/) for the ImageSharp API is available. This includes additional conceptual documentation to help you get started.
- Our [Samples Repository](https://github.com/SixLabors/Samples/tree/master/ImageSharp) is also available containing buildable code samples demonstrating common activities.

## Questions

- Do you have questions? We are happy to help! Please [join our Gitter channel](https://gitter.im/ImageSharp/General), or ask them on [Stack Overflow](https://stackoverflow.com) using the `ImageSharp` tag. Please do not open issues for questions.
- Please read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/master/.github/CONTRIBUTING.md) before opening issues or pull requests!

## Code of Conduct  
This project has adopted the code of conduct defined by the [Contributor Covenant](https://contributor-covenant.org/) to clarify expected behavior in our community.
For more information, see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## Installation 

Install stable releases via Nuget; development releases are available via MyGet.

| Package Name                   | Release (NuGet) | Nightly (MyGet) |
|--------------------------------|-----------------|-----------------|
| `SixLabors.ImageSharp`         | [![NuGet](https://img.shields.io/nuget/v/SixLabors.ImageSharp.svg)](https://www.nuget.org/packages/SixLabors.ImageSharp/) | [![MyGet](https://img.shields.io/myget/sixlabors/v/SixLabors.ImageSharp.svg)](https://www.myget.org/feed/sixlabors/package/nuget/SixLabors.ImageSharp) |

## Manual build

If you prefer, you can compile ImageSharp yourself (please do and help!)

- Using [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
  - Make sure you have the latest version installed
  - Make sure you have [the .NET Core 3.1 SDK](https://www.microsoft.com/net/core#windows) installed

Alternatively, you can work from command line and/or with a lightweight editor on **both Linux/Unix and Windows**:

- [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
- [.NET Core](https://www.microsoft.com/net/core#linuxubuntu)

To clone ImageSharp locally, click the "Clone in [YOUR_OS]" button above or run the following git commands:

```bash
git clone https://github.com/SixLabors/ImageSharp
```

If working with Windows please ensure that you have enabled log file paths in git (run as Administrator).

```bash
git config --system core.longpaths true
```

This repository contains [git submodules](https://blog.github.com/2016-02-01-working-with-submodules/). To add the submodules to the project, navigate to the repository root and type:

``` bash
git submodule update --init --recursive
```

## How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests, no input is too little. Make sure to read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/master/.github/CONTRIBUTING.md) before opening a PR.

## The ImageSharp Team

- [James Jackson-South](https://github.com/jimbobsquarepants)
- [Dirk Lemstra](https://github.com/dlemstra)
- [Anton Firsov](https://github.com/antonfirsov)
- [Scott Williams](https://github.com/tocsoft)
- [Brian Popow](https://github.com/brianpopow)
