
# <img src="https://raw.githubusercontent.com/SixLabors/ImageSharp/master/build/icons/imagesharp-logo-256.png" alt="ImageSharp" width="52"/> ImageSharp

**ImageSharp** is a new, fully featured, fully managed, cross-platform, 2D graphics API designed to allow the processing of images without the use of `System.Drawing`. 

Built against .Net Standard 1.1 ImageSharp can be used in device, cloud, and embedded/IoT scenarios. 

> **ImageSharp** has made excellent progress and contains many great features but is still considered by us to be still in early stages (alpha). As such, we cannot support its use on production environments until the library reaches release candidate status.
>
> Pre-release downloads are available from the [MyGet package repository](https://www.myget.org/gallery/imagesharp).

[![GitHub license](https://img.shields.io/badge/license-Apache%202-blue.svg)](https://raw.githubusercontent.com/SixLabors/ImageSharp/master/APACHE-2.0-LICENSE.txt)
[![GitHub issues](https://img.shields.io/github/issues/SixLabors/ImageSharp.svg)](https://github.com/SixLabors/ImageSharp/issues)
[![GitHub stars](https://img.shields.io/github/stars/SixLabors/ImageSharp.svg)](https://github.com/SixLabors/ImageSharp/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/SixLabors/ImageSharp.svg)](https://github.com/SixLabors/ImageSharp/network)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/ImageSharp/General?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Twitter](https://img.shields.io/twitter/url/https/github.com/SixLabors/ImageSharp.svg?style=social)](https://twitter.com/intent/tweet?hashtags=imagesharp,dotnet,oss&text=ImageSharp.+A+new+cross-platform+2D+graphics+API+in+C%23&url=https%3a%2f%2fgithub.com%2fSixLabors%2fImageSharp&via=sixlabors)
[![OpenCollective](https://opencollective.com/imagesharp/backers/badge.svg)](#backers) 
[![OpenCollective](https://opencollective.com/imagesharp/sponsors/badge.svg)](#sponsors)



|             |Build Status|Code Coverage|
|-------------|:----------:|:-----------:|
|**Linux/Mac**|[![Build Status](https://travis-ci.org/SixLabors/ImageSharp.svg)](https://travis-ci.org/SixLabors/ImageSharp)|[![Code coverage](https://codecov.io/gh/SixLabors/ImageSharp/branch/master/graph/badge.svg)](https://codecov.io/gh/SixLabors/ImageSharp)|
|**Windows**  |[![Build Status](https://ci.appveyor.com/api/projects/status/m9pn907xdah3ca39/branch/master?svg=true)](https://ci.appveyor.com/project/six-labors/imagesharp/branch/master)|[![Code coverage](https://codecov.io/gh/SixLabors/ImageSharp/branch/master/graph/badge.svg)](https://codecov.io/gh/SixLabors/ImageSharp)|


### Installation
At present the code is pre-release but when ready it will be available on [Nuget](http://www.nuget.org). 

**Pre-release downloads**

We already have a [MyGet package repository](https://www.myget.org/gallery/imagesharp) - for bleeding-edge / development NuGet releases.

### Packages

The **ImageSharp** library is made up of multiple packages.

Packages include:
- **ImageSharp**
  - Contains the generic `Image<TPixel>` class, PixelFormats, Primitives, Configuration, and other core functionality.
  - The `IImageFormat` interface, Jpeg, Png, Bmp, and Gif formats.
  - Transform methods like Resize, Crop, Skew, Rotate - Anything that alters the dimensions of the image.
  - Non-transform methods like Gaussian Blur, Pixelate, Edge Detection - Anything that maintains the original image dimensions.

- **ImageSharp.Drawing**
  - Brushes and various drawing algorithms, including drawing images.
  - Various vector drawing methods for drawing paths, polygons etc.
  - Text drawing.

### Manual build

If you prefer, you can compile ImageSharp yourself (please do and help!), you'll need:

- [Visual Studio 2017 (or above)](https://www.visualstudio.com/en-us/news/releasenotes/vs2017-relnotes)
- The [.NET Core SDK Installer](https://www.microsoft.com/net/core#windows) - Non VSCode link.

Alternatively on Linux you can use:

- [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
- [.Net Core](https://www.microsoft.com/net/core#linuxubuntu)

To clone it locally click the "Clone in Windows" button above or run the following git commands.

```bash
git clone https://github.com/SixLabors/ImageSharp
```

### Features

There's plenty there and more coming. Check out the [current features](features.md)!

### API 

Without the constraints of `System.Drawing` We have been able to develop something much more flexible, easier to code against, and much, much less prone to memory leaks.

Gone are system-wide process-locks; ImageSharp images are thread-safe and fully supported in web environments.

Many `Image<TPixel>` methods are also fluent.

Here's an example of the code required to resize an image using the default Bicubic resampler then turn the colors into their grayscale equivalent using the BT709 standard matrix.

`Rgba32` is our default PixelFormat, equivalent to `System.Drawing Color`.

On platforms supporting netstandard 1.3+
```csharp
// Image.Load(string path) is a shortcut for our default type. Other pixel formats use Image.Load<TPixel>(string path))
using (Image<Rgba32> image = Image.Load("foo.jpg"))
{
    image.Resize(image.Width / 2, image.Height / 2)
         .Grayscale()
         .Save("bar.jpg"); // automatic encoder selected based on extension.
}
```
on netstandard 1.1 - 1.2
```csharp
// Image.Load(Stream stream) is a shortcut for our default type. Other pixel formats use Image.Load<TPixel>(Stream stream))
using (FileStream stream = File.OpenRead("foo.jpg"))
using (FileStream output = File.OpenWrite("bar.jpg"))
using (Image<Rgba32> image = Image.Load<Rgba32>(stream))
{
    image.Resize(image.Width / 2, image.Height / 2)
         .Grayscale()
         .Save(output);
}
```

Setting individual pixel values can be perfomed as follows:

```csharp
// Individual pixels
using (Image<Rgba32> image = new Image<Rgba32>(400, 400))
{
    image[200, 200] = Rgba32.White;
}
```

For optimized access within a loop it is recommended that the following methods are used.

1. `image.GetRowSpan(y)`
2. `image.GetRowSpan(x, y)`

For advanced pixel format usage there are multiple [PixelFormat implementations](https://github.com/SixLabors/ImageSharp/tree/master/src/ImageSharp/PixelFormats) available allowing developers to implement their own color models in the same manner as Microsoft XNA Game Studio and MonoGame. 

All in all this should allow image processing to be much more accessible to developers which has always been my goal from the start.

### How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests. 

Performance is a biggie, if you know anything about the `System.Numerics.Vectors` types and can apply some fancy new stuff with that it would be awesome. 

There's a lot of developers out there who could write this stuff a lot better and faster than I and I would love to see what we collectively can come up with so please, if you can help in any way it would be most welcome and benificial for all.

### The ImageSharp Team

Grand High Eternal Dictator
- [James Jackson-South](https://github.com/jimbobsquarepants)

Core Team
- [Dirk Lemstra](https://github.com/dlemstra)
- [Jeavon Leopold](https://github.com/jeavon)
- [Anton Firsov](https://github.com/antonfirsov)
- [Olivia Ifrim](https://github.com/olivif)
- [Scott Williams](https://github.com/tocsoft)

### Backers

Support us with a monthly donation and help us continue our activities. [[Become a backer](https://opencollective.com/imagesharp#backer)]

<a href="https://opencollective.com/imagesharp/backer/0/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/0/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/1/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/1/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/2/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/2/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/3/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/3/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/4/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/4/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/5/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/5/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/6/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/6/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/7/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/7/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/8/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/8/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/9/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/9/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/10/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/10/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/11/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/11/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/12/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/12/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/13/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/13/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/14/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/14/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/15/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/15/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/16/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/16/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/17/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/17/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/18/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/18/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/19/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/19/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/20/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/20/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/21/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/21/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/22/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/22/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/23/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/23/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/24/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/24/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/25/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/25/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/26/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/26/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/27/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/27/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/28/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/28/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/backer/29/website" target="_blank"><img src="https://opencollective.com/imagesharp/backer/29/avatar.svg"></a>

### Sponsors

Become a sponsor and get your logo on our README on Github with a link to your site. [[Become a sponsor](https://opencollective.com/imagesharp#sponsor)]

<a href="https://opencollective.com/imagesharp/sponsor/0/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/0/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/1/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/1/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/2/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/2/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/3/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/3/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/4/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/4/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/5/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/5/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/6/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/6/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/7/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/7/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/8/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/8/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/9/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/9/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/10/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/10/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/11/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/11/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/12/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/12/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/13/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/13/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/14/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/14/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/15/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/15/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/16/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/16/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/17/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/17/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/18/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/18/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/19/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/19/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/20/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/20/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/21/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/21/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/22/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/22/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/23/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/23/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/24/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/24/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/25/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/25/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/26/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/26/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/27/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/27/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/28/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/28/avatar.svg"></a>
<a href="https://opencollective.com/imagesharp/sponsor/29/website" target="_blank"><img src="https://opencollective.com/imagesharp/sponsor/29/avatar.svg"></a>
