
# <img src="build/icons/imagesharp-logo-64.png" width="48" height="48"/> ImageSharp

**ImageSharp** is a new cross-platform 2D graphics API designed to allow the processing of images without the use of `System.Drawing`. 

> **ImageSharp is still in early stages (alpha) but progress has been pretty quick. As such, please do not use on production environments until the library reaches release candidate status. Pre-release downloads are available from the [MyGet package repository](https://www.myget.org/gallery/imagesharp).**

[![GitHub license](https://img.shields.io/badge/license-Apache%202-blue.svg)](https://raw.githubusercontent.com/JimBobSquarePants/ImageSharp/master/APACHE-2.0-LICENSE.txt)
[![Build status](https://ci.appveyor.com/api/projects/status/hu6d1gdpxdw0q360/branch/master?svg=true)](https://ci.appveyor.com/project/JamesSouth/imagesharp/branch/master)
[![GitHub issues](https://img.shields.io/github/issues/JimBobSquarePants/ImageSharp.svg)](https://github.com/JimBobSquarePants/ImageSharp/issues)
[![GitHub stars](https://img.shields.io/github/stars/JimBobSquarePants/ImageSharp.svg)](https://github.com/JimBobSquarePants/ImageSharp/stargazers)
[![GitHub forks](https://img.shields.io/github/forks/JimBobSquarePants/ImageSharp.svg)](https://github.com/JimBobSquarePants/ImageSharp/network)
[![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/ImageSharp/General?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Twitter](https://img.shields.io/twitter/url/https/github.com/JimBobSquarePants/ImageSharp.svg?style=social)](https://twitter.com/intent/tweet?hashtags=imagesharp,dotnet,oss&text=ImageSharp.+A+new+cross-platform+2D+graphics+API+in+C%23&url=https%3a%2f%2fgithub.com%2fJimBobSquarePants%2fImageSharp&via=james_m_south)

### Installation
At present the code is pre-release but when ready it will be available on [Nuget](http://www.nuget.org). 

**Pre-release downloads**

We already have a [MyGet package repository](https://www.myget.org/gallery/imagesharp) - for bleeding-edge / development NuGet releases.

### Manual build

If you prefer, you can compile ImageSharp yourself (please do and help!), you'll need:

- [Visual Studio 2015 with Update 3 (or above)](https://www.visualstudio.com/news/releasenotes/vs2015-update3-vs)
- The [.NET Core 1.0 SDK Installer](https://www.microsoft.com/net/core#windows) - Non VSCode link.

To clone it locally click the "Clone in Windows" button above or run the following git commands.

```bash
git clone https://github.com/JimBobSquarePants/ImageSharp
```

### What works so far/ What is planned?

- Encoding/decoding of image formats (plugable).
 - [x] Jpeg (Includes Subsampling. Progressive writing required)
 - [x] Bmp (Read: 32bit, 24bit, 16 bit. Write: 32bit, 24bit just now)
 - [x] Png (Read: Rgb, Rgba, Grayscale, Grayscale + alpha, Palette. Write: Rgb, Rgba, Grayscale, Grayscale + alpha, Palette) Needs interlaced decoding 
 - [x] Gif (Includes animated)
 - [ ] Tiff
- Metadata
 - [x] EXIF Read/Write (Jpeg just now)
- Quantizers (IQuantizer with alpha channel support + thresholding)
 - [x] Octree
 - [x] Xiaolin Wu
 - [x] Palette
- Basic color structs with implicit operators. 
 - [x] Color - 32bit color in RGBA order (IPackedPixel\<TPacked\>).
 - [x] Bgra32
 - [x] CIE Lab
 - [x] CIE XYZ
 - [x] CMYK
 - [x] HSV
 - [x] HSL
 - [x] YCbCr
- IPackedPixel\<TPacked\> representations of color models. Compatible with Microsoft XNA Game Studio and MonoGame.
 - [x] Alpha8 
 - [x] Argb 
 - [x] Bgr565 
 - [x] Bgra444 
 - [x] Bgra565 
 - [x] Byte4 
 - [x] HalfSingle 
 - [x] HalfVector2 
 - [x] HalfVector4 
 - [x] NormalizedByte2 
 - [x] NormalizedByte4 
 - [x] NormalizedShort2 
 - [x] NormalizedShort4 
 - [x] Rg32 
 - [x] Rgba1010102 
 - [x] Rgba64 
 - [x] Short2 
 - [x] Short4 
- Basic shape primitives.
 - [x] Rectangle
 - [x] Size
 - [x] Point
 - [x] Ellipse
- Resampling algorithms. (Optional gamma correction, resize modes, Performance improvements?)
 - [x] Box
 - [x] Bicubic
 - [x] Lanczos2
 - [x] Lanczos3
 - [x] Lanczos5
 - [x] Lanczos8
 - [x] MitchelNetravali
 - [x] Nearest Neighbour 
 - [x] Robidoux
 - [x] Robidoux Sharp
 - [x] Spline
 - [x] Triangle
 - [x] Welch
- Padding 
 - [x] Pad
 - [x] ResizeMode.Pad
 - [x] ResizeMode.BoxPad
- Cropping
 - [x] Rectangular Crop
 - [ ] Elliptical Crop
 - [x] Entropy Crop
 - [x] ResizeMode.Crop
- Rotation/Skew
 - [x] Flip (90, 270, FlipType etc)
 - [x] Rotate by angle and center point (Expandable canvas).
 - [x] Skew by x/y angles and center point (Expandable canvas).
- ColorMatrix operations (Uses Matrix4x4)
 - [x] BlackWhite
 - [x] Grayscale BT709
 - [x] Grayscale BT601
 - [x] Hue
 - [x] Saturation
 - [x] Lomograph
 - [x] Polaroid
 - [x] Kodachrome
 - [x] Sepia
 - [x] Achromatomaly 
 - [x] Achromatopsia
 - [x] Deuteranomaly
 - [x] Deuteranopia
 - [x] Protanomaly
 - [x] Protanopia
 - [x] Tritanomaly
 - [x] Tritanopia
- Edge Detection
 - [x] Kayyali
 - [x] Kirsch
 - [x] Laplacian3X3
 - [x] Laplacian5X5
 - [x] LaplacianOfGaussian
 - [x] Prewitt
 - [x] RobertsCross
 - [x] Robinson
 - [x] Scharr
 - [x] Sobel
- Blurring/Sharpening
 - [x] Gaussian blur
 - [x] Gaussian sharpening
 - [x] Box Blur
- Filters
 - [x] Alpha
 - [x] Contrast
 - [x] Invert
 - [x] BackgroundColor
 - [x] Brightness
 - [x] Pixelate
 - [x] Blend
 - [ ] Mask
 - [x] Oil Painting
 - [x] Vignette
 - [x] Glow
 - [x] Threshold
- Drawing
 - [ ] Path brush (Need help) 
 - [ ] Hatch brush (Need help)
 - [ ] Elliptical brush (Need help)
 - [ ] Gradient brush (Need help)
- Other stuff I haven't thought of.
 
### What might never happen
- Font support. I don't know where to start coding this so if you have any pointers please chip in.

### API 

Without the constraints of `System.Drawing` I have been able to develop something much more flexible, easier to code against, and much, much less prone to memory leaks. Gone are system-wide process-locks. Images and processors are thread safe usable in parallel processing utilizing all the availables cores. 

Many `Image` methods are also fluent.

Here's an example of the code required to resize an image using the default Bicubic resampler then turn the colors into their grayscale equivalent using the BT709 standard matrix.

```csharp
using (FileStream stream = File.OpenRead("foo.jpg"))
using (FileStream output = File.OpenWrite("bar.jpg"))
{
    Image image = new Image(stream);
    image.Resize(image.Width / 2, image.Height / 2)
         .Grayscale()
         .Save(output);
}
```

Individual processors can be initialised and apply processing against images. This allows nesting which brings the potential for powerful combinations of processing methods:

```csharp
new BrightnessProcessor(50).Apply(sourceImage, sourceImage.Bounds);
```

Setting individual pixel values is perfomed as follows:

```csharp
Image image = new Image(400, 400);
using (PixelAccessor pixels = image.Lock())
{
    pixels[200, 200] = Color.White;
}
```

For advanced usage the `Image<TColor, TPacked>` and `PixelAccessor<TColor, TPacked>` classes are available allowing developers to implement their own color models in the same manner as Microsoft XNA Game Studio and MonoGame. 

All in all this should allow image processing to be much more accessible to developers which has always been my goal from the start.

### How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests. 

Performance is a biggie, if you know anything about the new vector types and can apply some fancy new stuff with that it would be awesome. 

There's a lot of developers out there who could write this stuff a lot better and faster than I and I would love to see what we collectively can come up with so please, if you can help in any way it would be most welcome and benificial for all.

### The ImageSharp Team

Grand High Eternal Dictator
- [James Jackson-South](https://github.com/jimbobsquarepants)

Core Team
- [Dirk Lemstra](https://github.com/dlemstra)
- [Jeavon Leopold](https://github.com/jeavon)
