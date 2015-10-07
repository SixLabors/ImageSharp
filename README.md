# ImageProcessor

**This branch contains the highly experimental cross platform version of ImageProcessor**.

This is a complete rewrite from the ground up to allow the processing of images without the use of `System.Drawing` using a portable class library (PCL).

###Why am I writing this?

With NETCore there is currently no version of `System.Drawing` to allow continued progress of the existing ImageProcessor library. Progress developing a crossplatform update are restricted to the [CoreFXLab repo](https://github.com/dotnet/corefxlab/tree/master/src/System.Drawing.Graphics) where progress seems to be very slow.

###Am I mad?

Honestly... I don't know. I could be writing code that may be suddenly obsolete. There has been little [feedback](https://github.com/dotnet/corefxlab/issues/86#issuecomment-139930600) on questions I've asked but it's a nice learning process if anything.

###What works so far?

- Encoding/decoding of jpeg, bmp, png, and gif formats (Needs expansion for indexed pngs, more bmp formats)
- Basic color structs (Needs support for HDR colors etc). 
- Basic shape primitives (Unfinished and could possible be updated by using Vector2, Vector3 etc)
- Bicubic resampling. (Needs more algorithms & performance tweaks)

###How can you help?

Spread the word, contribute algorithms, performance improvements, unit tests. Help me setup the solution properly for NETCore etc (I dunno if I have my setup correct) 

There's a lot of people out there who could write this stuff a lot better and faster than I and would love to get a little more sleep once in a while.
