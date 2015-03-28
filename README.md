# ImageProcessor

### Build Status

|Branch   |         |
|:--------|:--------|
|**Debug**|[![Build status](https://ci.appveyor.com/api/projects/status/8ypr7527dnao04yr?svg=true)](https://ci.appveyor.com/project/JamesSouth/imageprocessor)|
|**Release**|[![Build status](https://ci.appveyor.com/api/projects/status/8ypr7527dnao04yr/branch/Master?svg=true)](https://ci.appveyor.com/project/JamesSouth/imageprocessor/branch/Master)|
|**Coverage Report**|[![Coverage Status](https://coveralls.io/repos/JimBobSquarePants/ImageProcessor/badge.svg)](https://coveralls.io/r/JimBobSquarePants/ImageProcessor?branch=V2)|

### Latest Releases
|Library           |Version           |Downloads         |
|:-----------------|:-----------------|:-----------------|
|**ImageProcessor**|[![Nuget count](http://img.shields.io/nuget/v/ImageProcessor.svg)](https://www.nuget.org/packages/ImageProcessor/)|[![Nuget downloads](http://img.shields.io/nuget/dt/ImageProcessor.svg)](https://www.nuget.org/packages/ImageProcessor/)|
|**ImageProcessor.Web**|[![Nuget count](http://img.shields.io/nuget/v/ImageProcessor.Web.svg)](https://www.nuget.org/packages/ImageProcessor.Web/)|[![Nuget downloads](http://img.shields.io/nuget/dt/ImageProcessor.Web.svg)](https://www.nuget.org/packages/ImageProcessor.Web/)|

[![Issues open](http://img.shields.io/github/issues-raw/JimBobSquarePants/imageprocessor.svg)](https://huboard.com/JimBobSquarePants/ImageProcessor/)
[![Source Browser](https://img.shields.io/badge/Browse-Source-green.svg)](http://sourcebrowser.io/Browse/JimBobSquarePants/ImageProcessor/)
[![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/JimBobSquarePants/ImageProcessor?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

Imageprocessor is a lightweight, extensible library written in C# that allows you to manipulate images on-the-fly using .NET 4.5+

It's fast, extensible, easy to use, comes bundled with some great features and is fully open source.

For full documentation please see [http://imageprocessor.org/](http://imageprocessor.org/)

## Contributing to ImageProcessor
Contribution is most welcome! I mean, that's what this is all about isn't it?

Things I would :heart: people to help me out with:

 - Unit tests. I need to get some going for all the regular expressions within the ImageProcessor core plus anywhere else we can think of. If that's your bag please contribute.
 - Documentation. Nobody likes doing docs, I've written a lot but my prose is clumsy and verbose. If you think you can make things clearer or you spot any mistakes please submit a pull request (Guide to how the docs work below).

**Just remember to StyleCop all things! :oncoming_police_car:**

## RoadMap
I want the next version of ImageProcessor to run on all devices. Sadly it looks like using `System.Drawing` will not allow me to do that so I need to have a look at using an alternative. This is a lot of work so any help that could be offered would be greatly appreciated.

## Documentation

ImageProcessor's documentation, included in this repo in the gh-pages branch, is built with [Jekyll](http://jekyllrb.com) and publicly hosted on GitHub Pages at <http://imageprocessor.org>. The docs may also be run locally.

### Running documentation locally
1. If necessary, [install Jekyll](http://jekyllrb.com/docs/installation) (requires v2.2.x).
  - **Windows users:** Read [this unofficial guide](https://github.com/juthilo/run-jekyll-on-windows/) to get Jekyll up and running without problems. 
2. From the root `/ImageProcessor` directory, run `jekyll serve` in the command line.
3. Open <http://localhost:4000> in your browser to navigate to your site.
Learn more about using Jekyll by reading its [documentation](http://jekyllrb.com/docs/home/).
