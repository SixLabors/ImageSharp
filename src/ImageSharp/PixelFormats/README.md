Pixel formats adapted and extended from:

https://github.com/MonoGame/MonoGame

Rgba32 is our default format. As such it positioned within the ImageSharp root namespace to ensure visibility of the format.

All other pixel formats should be positioned within ImageSharp.PixelFormats to reduce intellisense burden.

The naming convention of each pixel format is to order the color components from least significant to most significant, reading from left to right.
For example in the Rgba32 pixel format the R component is the least significant byte, and the A component is the most significant.
