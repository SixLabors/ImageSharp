<h1 align="center">

<img src="https://raw.githubusercontent.com/SixLabors/Branding/master/icons/imagesharp/sixlabors.imagesharp.512.png" alt="SixLabors.ImageSharp" width="256"/>
<br/>
SixLabors.ImageSharp
</h1>


<div align="center">

[![GitHub license](https://img.shields.io/badge/license-Apache%202-blue.svg)](https://raw.githubusercontent.com/SixLabors/ImageSharp/master/LICENSE)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/ImageSharp/General?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Twitter](https://img.shields.io/twitter/url/http/shields.io.svg?style=flat&logo=twitter)](https://twitter.com/intent/tweet?hashtags=imagesharp,dotnet,oss&text=ImageSharp.+A+new+cross-platform+2D+graphics+API+in+C%23&url=https%3a%2f%2fgithub.com%2fSixLabors%2fImageSharp&via=sixlabors)
[![OpenCollective](https://opencollective.com/imagesharp/backers/badge.svg)](#backers)
[![OpenCollective](https://opencollective.com/imagesharp/sponsors/badge.svg)](#sponsors)

</div>

### **ImageSharp** is a new, fully featured, fully managed, cross-platform, 2D graphics API. 

Designed to democratize image processing, ImageSharp brings you an incredibly powerful yet beautifully simple API.

Compared to `System.Drawing` we have been able to develop something much more flexible, easier to code against, and much, much less prone to memory leaks. Gone are system-wide process-locks; ImageSharp images are thread-safe and fully supported in web environments.

Built against .Net Standard 1.1 ImageSharp can be used in device, cloud, and embedded/IoT scenarios. 

### Documentation
For all SixLabors projects, including ImageSharp:
https://sixlabors.github.io/docs/

### Installation

Install stable releases via Nuget; development releases are available via MyGet.

| Package Name                   | Release (NuGet) | Nightly (MyGet) |
|--------------------------------|-----------------|-----------------|
| `SixLabors.ImageSharp`         | [![NuGet](https://img.shields.io/nuget/v/SixLabors.ImageSharp.svg)](https://www.nuget.org/packages/SixLabors.ImageSharp/) | [![MyGet](https://img.shields.io/myget/sixlabors/v/SixLabors.ImageSharp.svg)](https://www.myget.org/feed/sixlabors/package/nuget/SixLabors.ImageSharp) |
| `SixLabors.ImageSharp.Drawing` | [![NuGet](https://img.shields.io/nuget/v/SixLabors.ImageSharp.Drawing.svg)](https://www.nuget.org/packages/SixLabors.ImageSharp.Drawing/) | [![MyGet](https://img.shields.io/myget/sixlabors/v/SixLabors.ImageSharp.Drawing.svg)](https://www.myget.org/feed/sixlabors/package/nuget/SixLabors.ImageSharp.Drawing) |

### Packages

The **ImageSharp** library is made up of multiple packages:
- **SixLabors.ImageSharp**
  - Contains the generic `Image<TPixel>` class, PixelFormats, Primitives, Configuration, and other core functionality.
  - The `IImageFormat` interface, Jpeg, Png, Bmp, and Gif formats.
  - Transform methods like Resize, Crop, Skew, Rotate - Anything that alters the dimensions of the image.
  - Non-transform methods like Gaussian Blur, Pixelate, Edge Detection - Anything that maintains the original image dimensions.

- **SixLabors.ImageSharp.Drawing**
  - Brushes and various drawing algorithms, including drawing images.
  - Various vector drawing methods for drawing paths, polygons etc.
  - Text drawing.

### Build Status

|             |Build Status|Code Coverage|
|-------------|:----------:|:-----------:|
|**Linux/Mac**|[![Build Status](https://travis-ci.org/SixLabors/ImageSharp.svg)](https://travis-ci.org/SixLabors/ImageSharp)|[![Code coverage](https://codecov.io/gh/SixLabors/ImageSharp/branch/master/graph/badge.svg)](https://codecov.io/gh/SixLabors/ImageSharp)|
|**Windows**  |[![Build Status](https://ci.appveyor.com/api/projects/status/m9pn907xdah3ca39/branch/master?svg=true)](https://ci.appveyor.com/project/six-labors/imagesharp/branch/master)|[![Code coverage](https://codecov.io/gh/SixLabors/ImageSharp/branch/master/graph/badge.svg)](https://codecov.io/gh/SixLabors/ImageSharp)|

### Questions?

- Do you have questions? We are happy to help! Please [join our gitter channel](https://gitter.im/ImageSharp/General), or ask them on [stackoverflow](https://stackoverflow.com) using the `ImageSharp` tag. **Do not** open issues for questions!
- Please read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/master/.github/CONTRIBUTING.md) before opening issues or pull requests!

### API 

Our API is designed to be simple to consume. Here's an example of the code required to resize an image using the default Bicubic resampler then turn the colors into their grayscale equivalent using the BT709 standard matrix.

On platforms supporting netstandard 1.3+

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

// Image.Load(string path) is a shortcut for our default type. 
// Other pixel formats use Image.Load<TPixel>(string path))
using (Image<Rgba32> image = Image.Load("foo.jpg"))
{
    image.Mutate(x => x
         .Resize(image.Width / 2, image.Height / 2)
         .Grayscale());
    image.Save("bar.jpg"); // Automatic encoder selected based on extension.
}
```
On netstandard 1.1 - 1.2

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

// Image.Load(Stream stream) is a shortcut for our default type.
// Other pixel formats use Image.Load<TPixel>(Stream stream))
using (FileStream stream = File.OpenRead("foo.jpg"))
using (FileStream output = File.OpenWrite("bar.jpg"))
using (Image<Rgba32> image = Image.Load(stream))
{
    image.Mutate(x => x
         .Resize(image.Width / 2, image.Height / 2)
         .Grayscale());
    image.Save(output);
}
```

Setting individual pixel values can be performed as follows:

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Individual pixels
using (Image<Rgba32> image = new Image<Rgba32>(400, 400))
{
    image[200, 200] = Rgba32.White;
}
```

`Rgba32` is our default PixelFormat, equivalent to `System.Drawing Color`. For advanced pixel format usage there are multiple [PixelFormat implementations](https://github.com/SixLabors/ImageSharp/tree/master/src/ImageSharp/PixelFormats) available allowing developers to implement their own color models in the same manner as Microsoft XNA Game Studio and MonoGame. 

For more examples check out: 
- [Our Documentation](https://sixlabors.github.io/docs/)
- Our [Samples Repository](https://github.com/SixLabors/Samples/tree/master/ImageSharp)
- The [beta1 blog post](https://sixlabors.com/blog/announcing-imagesharp-beta-1/)

### Manual build

If you prefer, you can compile ImageSharp yourself (please do and help!)

- Using [Visual Studio 2017](https://visualstudio.microsoft.com/vs/)
  - Make sure you have the latest version installed
  - Make sure you have [the .NET Core 2.1 SDK](https://www.microsoft.com/net/core#windows) installed

Alternatively, you can work from command line and/or with a lightweight editor on **both Linux/Unix and Windows**:

- [Visual Studio Code](https://code.visualstudio.com/) with [C# Extension](https://marketplace.visualstudio.com/items?itemName=ms-vscode.csharp)
- [.Net Core](https://www.microsoft.com/net/core#linuxubuntu)

To clone ImageSharp locally click the "Clone in Windows" button above or run the following git commands.

```bash
git clone https://github.com/SixLabors/ImageSharp
```

### How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests, no input is too little. Make sure to read our [Contribution Guide](https://github.com/SixLabors/ImageSharp/blob/master/.github/CONTRIBUTING.md) before opening a PR.

### The ImageSharp Team

Grand High Eternal Dictator
- [James Jackson-South](https://github.com/jimbobsquarepants)

Core Team
- [Dirk Lemstra](https://github.com/dlemstra)
- [Anton Firsov](https://github.com/antonfirsov)
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
