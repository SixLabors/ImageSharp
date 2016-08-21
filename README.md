
# ImageProcessorCore

<img src="build/icons/imageprocessor-logo-512.png" width="128" height="128"/>

**ImageProcessorCore** is a new cross-platform 2D graphics API designed to allow the processing of images without the use of `System.Drawing`. 

> **ImageProcessorCore is still in early stages (alpha) but progress has been pretty quick. As such, please do not use on production environments until the library reaches release candidate status.**

> For the older `ImageFactory` based API that uses `System.Drawing` please check out the [Framework](https://github.com/JimBobSquarePants/ImageProcessor/tree/Framework) branch.


[![Build status](https://ci.appveyor.com/api/projects/status/8ypr7527dnao04yr/branch/Core?svg=true)](https://ci.appveyor.com/project/JamesSouth/imageprocessor/branch/Core)
[![Gitter](https://badges.gitter.im/Join Chat.svg)](https://gitter.im/JimBobSquarePants/ImageProcessor?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)


---
## ImageProcessor Needs Your Help

**ImageProcessor is the work of a very, very, small number of developers who struggle balancing time to contribute to the project with family time and work commitments. If the project is to be successful we need more contribution from the community at large. There are several issues, most notably [#264](https://github.com/JimBobSquarePants/ImageProcessor/issues/264) and [#347](https://github.com/JimBobSquarePants/ImageProcessor/issues/446) that we cannot possibly solve on our own.**

**We, and we believe many others in the community at large want a first-class 2D imaging library with a simple API that is not simply a wrapper round an existing library. We want it to have a low contribution bar which we believe can only happen if the library is written in C#. We want it to be written to cover as many use cases as possible. We want to write the same code once and have it work on any platform supporting .NET Core.**

**With your help we can make all that a reality.**

**If you can donate any time to improve on the project, be it helping with documentation, tests or contributing code please do.**

**Thankyou for reading this**
---

### Installation
At present the code is pre-release but when ready it will be available on [Nuget](http://www.nuget.org). 

**Pre-release downloads**

We already have a [MyGet package repository](https://www.myget.org/gallery/imageprocessor) - for bleeding-edge / development NuGet releases.

### Manual build

If you prefer, you can compile ImageProcessorCore yourself (please do and help!), you'll need:

- [Visual Studio 2015 with Update 3 (or above)](https://www.visualstudio.com/news/releasenotes/vs2015-update3-vs)
- The [.NET Core 1.0 SDK Installer](https://www.microsoft.com/net/core#windows) - Non VSCode link.

To clone it locally click the "Clone in Windows" button above or run the following git commands.

```bash
git clone https://github.com/JimBobSquarePants/ImageProcessor
```

###What works so far/ What is planned?

- Encoding/decoding of image formats (plugable).
 - [x] Jpeg (Includes Subsampling. Progressive writing required)
 - [x] Bmp (Read: 32bit, 24bit, 16 bit. Write: 32bit, 24bit just now)
 - [x] Png (Read: Rgb, Rgba, Grayscale, Grayscale + alpha, Palette. Write: Rgb, Rgba, Grayscale, Grayscale + alpha, Palette) Needs interlaced decoding [#379](https://github.com/JimBobSquarePants/ImageProcessor/issues/379)
 - [x] Gif (Includes animated)
 - [ ] Tiff
- Metadata
 - [x] EXIF Read/Write (Jpeg just now)
- Quantizers (IQuantizer with alpha channel support + thresholding)
 - [x] Octree
 - [x] Xiaolin Wu
 - [x] Palette
- Basic color structs with implicit operators. [#260](https://github.com/JimBobSquarePants/ImageProcessor/issues/260)
 - [x] Color - 32bit color in RGBA order.
 - [x] BGRA32
 - [x] CIE Lab
 - [x] CIE XYZ
 - [x] CMYK
 - [x] HSV
 - [x] HSL
 - [x] YCbCr
- Basic shape primitives (Vector backed)
 - [x] Rectangle (Doesn't contain all System.Drawing methods)
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
 - [x] Vignette
 - [x] Glow
 - [x] Threshold
- Effects
 - [ ] Path brush (Need help) [#264](https://github.com/JimBobSquarePants/ImageProcessor/issues/264)
 - [ ] Pattern brush (Need help) [#264](https://github.com/JimBobSquarePants/ImageProcessor/issues/264)
 - [ ] Elliptical brush (Need help) [#264](https://github.com/JimBobSquarePants/ImageProcessor/issues/264)
 - [ ] Gradient brush (vignette? Need help) [#264](https://github.com/JimBobSquarePants/ImageProcessor/issues/264)
- Other stuff I haven't thought of.
 
###What might never happen
- Font support (Depends on new System.Text stuff) I don't know where to start coding this so if you have any pointers please chip in.

###API Changes

With this version the API will change dramatically. Without the constraints of `System.Drawing` I have been able to develop something much more flexible, easier to code against, and much, much less prone to memory leaks. Gone are system wide proces locks with Images and processors thread safe usable in parallel processing utilizing all the availables cores. 

Image methods are also fluent which allow chaining much like the `ImageFactory` class in the Framework version.

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
new Brightness(50).Apply(sourceImage, targetImage, sourceImage.Bounds);
```

For advanced usage the `Image<T,TP>` class is available allowing developers to implement their own color models in the same manner as Microsoft XNA Game Studio and MonoGame 

All in all this should allow image processing to be much more accessible to developers which has always been my goal from the start.

###How can you help?

Please... Spread the word, contribute algorithms, submit performance improvements, unit tests. Help me set up CI for nightly releases. 

[Performance is a biggie](https://github.com/JimBobSquarePants/ImageProcessor/issues/347), if you know anything about the new vector types and can apply some fancy new stuff with that it would be awesome. 

There's a lot of developers out there who could write this stuff a lot better and faster than I and I would love to see what we collectively can come up with so please, if you can help in any way it would be most welcome and benificial for all.

###The ImageProcessor Team

Grand High Eternal Dictator
- [James Jackson-South](https://github.com/jimbobsquarepants)

Core Team
- [Jeavon Leopold](https://github.com/jeavon)
- [Dirk Lemstra](https://github.com/dlemstra)
- [Yufeih Huang](https://github.com/yufeih)
- [Thomas Broust](https://github.com/cosmo0)
- [Christopher Bauer](https://github.com/christopherbauer)

