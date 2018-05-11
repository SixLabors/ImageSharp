// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Identifies the colorspace of a Jpeg image
    /// </summary>
    internal enum JpegColorSpace
    {
        Undefined = 0,

        Grayscale,

        Ycck,

        Cmyk,

        RGB,

        YCbCr
    }
}