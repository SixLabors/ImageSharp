<h1 align="center">

<img src="https://github.com/SixLabors/Branding/raw/main/icons/imagesharp/sixlabors.imagesharp.svg?sanitize=true" alt="SixLabors.ImageSharp" width="256"/>
<br/>
SixLabors.ImageSharp
</h1>

<div align="center">

[![Build Status](https://img.shields.io/github/actions/workflow/status/SixLabors/ImageSharp/build-and-test.yml?branch=main)](https://github.com/SixLabors/ImageSharp/actions)
[![codecov](https://codecov.io/gh/SixLabors/ImageSharp/graph/badge.svg?token=g2WJwz770q)](https://codecov.io/gh/SixLabors/ImageSharp)
[![License: Six Labors Split](https://img.shields.io/badge/license-Six%20Labors%20Split-%23e30183)](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=flat&logo=twitter)](https://twitter.com/intent/tweet?hashtags=imagesharp,dotnet,oss&text=ImageSharp.+A+new+cross-platform+2D+graphics+API+in+C%23&url=https%3a%2f%2fgithub.com%2fSixLabors%2fImageSharp&via=sixlabors)

</div>

### **ImageSharp** is a new, fully featured, fully managed, cross-platform, 2D graphics API. 

ImageSharp is a new, fully featured, fully managed, cross-platform, 2D graphics library. 
Designed to simplify image processing, ImageSharp brings you an incredibly powerful yet beautifully simple API.

ImageSharp is designed from the ground up to be flexible and extensible. The library provides API endpoints for common image processing operations and the building blocks to allow for the development of additional operations.

Built against [.NET 8](https://docs.microsoft.com/en-us/dotnet/standard/net-standard), ImageSharp can be used in device, cloud, and embedded/IoT scenarios.


## License
  
- ImageSharp is licensed under the [Six Labors Split License, Version 1.0](https://github.com/SixLabors/ImageSharp/blob/main/LICENSE)  

## Support Six Labors

Support the efforts of the development of the Six Labors projects. 
 - [Purchase a Commercial License :heart:](https://sixlabors.com/pricing/)
 - [Become a sponsor via GitHub Sponsors :heart:]( https://github.com/sponsors/SixLabors)
 - [Become a sponsor via Open Collective :heart:](https://opencollective.com/sixlabors)

## Documentation

- [Detailed documentation](https://sixlabors.github.io/docs/) for the ImageSharp API is available. This includes additional conceptual documentation to help you get started.
- Our [Samples Repository](https://github.com/SixLabors/Samples/tree/main/ImageSharp) is also available containing buildable code samples demonstrating common activities.

## Questions

- Do you have questions? Please [join our Discussions Forum](https://github.com/SixLabors/ImageSharp/discussions/categories/q-a). Do not open issues for questions.
- For feature ideas please [join our Discussions Forum](https://github.com/SixLabors/ImageSharp/discussions/categories/ideas) and we'll be happy to discuss.  
- Please read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/main/.github/CONTRIBUTING.md) before opening issues or pull requests!

## Code of Conduct  
This project has adopted the code of conduct defined by the [Contributor Covenant](https://contributor-covenant.org/) to clarify expected behavior in our community.
For more information, see the [.NET Foundation Code of Conduct](https://dotnetfoundation.org/code-of-conduct).

## Installation 

Install stable releases via Nuget; development releases are available via MyGet.

| Package Name                   | Release (NuGet) | Nightly (Feedz.io) |
|--------------------------------|-----------------|-----------------|
| `SixLabors.ImageSharp`         | [![NuGet](https://img.shields.io/nuget/v/SixLabors.ImageSharp.svg)](https://www.nuget.org/packages/SixLabors.ImageSharp/) | [![feedz.io](https://img.shields.io/badge/endpoint.svg?url=https%3A%2F%2Ff.feedz.io%2Fsixlabors%2Fsixlabors%2Fshield%2FSixLabors.ImageSharp%2Flatest)](https://f.feedz.io/sixlabors/sixlabors/nuget/index.json) |

## Manual build

If you prefer, you can compile ImageSharp yourself (please do and help!)

- Using [Visual Studio 2022](https://visualstudio.microsoft.com/vs/)
  - Make sure you have the latest version installed
  - Make sure you have [the .NET 8 SDK](https://www.microsoft.com/net/core#windows) installed

Alternatively, you can work from command line and/or with a lightweight editor on **both Linux/Unix and Windows**:

- [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
- [.NET Core](https://www.microsoft.com/net/core#linuxubuntu)

To clone ImageSharp locally, click the "Clone in [YOUR_OS]" button above or run the following git commands:

```bash
git clone https://github.com/SixLabors/ImageSharp
```

Then set the following config to ensure blame commands ignore mass reformatting commits.

```bash
git config blame.ignoreRevsFile .git-blame-ignore-revs
```

If working with Windows please ensure that you have enabled long file paths in git (run as Administrator).

```bash
git config --system core.longpaths true
```

This repository uses [Git Large File Storage](https://docs.github.com/en/github/managing-large-files/installing-git-large-file-storage). Please follow the linked instructions to ensure you have it set up in your environment.

This repository contains [Git Submodules](https://blog.github.com/2016-02-01-working-with-submodules/). To add the submodules to the project, navigate to the repository root and type:

``` bash
git submodule update --init --recursive
```

## How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests, no input is too little. Make sure to read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/main/.github/CONTRIBUTING.md) before opening a PR.

Useful tools for development and links to specifications can be found in our wikipage: [Useful-tools-and-links](https://github.com/SixLabors/ImageSharp/wiki/Useful-tools-and-links).

## The ImageSharp Team

- [James Jackson-South](https://github.com/jimbobsquarepants)
- [Dirk Lemstra](https://github.com/dlemstra)
- [Anton Firsov](https://github.com/antonfirsov)
- [Scott Williams](https://github.com/tocsoft)
- [Brian Popow](https://github.com/brianpopow)

---

<div>
  <a href="https://www.jetbrains.com/?from=ImageSharp" align="right"><img src="https://resources.jetbrains.com/storage/products/company/brand/logos/jb_beam.svg" alt="JetBrains" class="logo-footer" width="72" align="left"></a>
  <br/>

  Special thanks to [JetBrains](https://www.jetbrains.com/?from=ImageSharp) for supporting us with open-source licenses for their IDEs.
</div>
