# ImageProcessor

**This branch contains the highly experimental cross platform version of ImageProcessor**.

This is a complete rewrite from the ground up to allow the processing of images without the use of `System.Drawing` using a portable class library (PCL).

###Why am I writing this?

With NETCore there is currently no version of `System.Drawing` to allow continued progress of the existing ImageProcessor library. Progress developing a crossplatform update are restricted to the [CoreFXLab repo](https://github.com/dotnet/corefxlab/tree/master/src/System.Drawing.Graphics) where progress seems to be very slow.

###Is this wise?

Honestly... I don't know. I could be writing code that may be suddenly obsolete. There has been little [feedback](https://github.com/dotnet/corefxlab/issues/86#issuecomment-139930600) on questions I've asked but it's a nice learning process if anything.

###What works so far/ What is planned?

- Encoding/decoding of image formats (plugable)
 - [x] jpeg (Includes progressive)
 - [x] bmp (More bmp format support required, 24bit just now)
 - [x] png (Need updating for indexed support)
 - [x] gif
- Basic color structs with implicit operators (Needs support for HDR colors etc, could possible be updated by using Vector3/Vector4).
 - [x] BGRA (Should this become RGBA?)
 - [ ] CIE Lab
 - [x] CMYK
 - [x] HSV
 - [ ] HSLA
 - [ ] RGBAW
 - [x] YCbCr
- Basic shape primitives (Unfinished and could possible be updated by using Vector2, Vector3, etc)
 - [x] Rectangle
 - [x] Size
 - [x] Point
- Resampling algorithms. (Performance improvements?)
 - [x] Box
 - [x] Bicubic
 - [x] Lanczos3
 - [x] Lanczos5
 - [x] Lanczos8
 - [x] MitchelNetravali
 - [ ] Nearest Neighbour
 - [x] Robidoux
 - [x] Robidoux Sharp
 - [x] Robidoux Soft
 - [x] Spline
 - [x] Triangle
 - [x] Welch
- Rotation
 - [ ] Flip (90, 270, FlipType etc) 
 - [ ] Rotate by angle
- ColorMatrix operations (Performance improvements? Following matrices implemented)
 - [x] BlackWhite
 - [x] Greyscale BT709
 - [x] Greyscale BT601
 - [x] Invert
 - [x] Lomograph
 - [x] Polaroid
 - [x] Sepia
- Blurring/ Sharpening
 - [ ] Gaussian blur
 - [ ] Gaussian sharpening
 - [ ] Box Blur
- Filters
 - [x] Alpha
 - [x] Contrast
 - [ ] Brightness
 - [ ] Saturation
 - [ ] Hue
- Effects
 - [ ] Pattern brushes
 - [ ] Elliptical brushes
 - [ ] Gradient brush (vignette?)
 
 
###What might never happen
- Font support (Depends on new System.Text stuff)

###How can you help?

Spread the word, contribute algorithms, performance improvements, unit tests. Help me setup the solution properly for NETCore etc (I dunno if I have my setup correct) 

There's a lot of developers out there who could write this stuff a lot better and faster than I and I would love to see what people can come up with.
