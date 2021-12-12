// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder
{
    /// <summary>
    /// Identifies the colorspace of a Jpeg image.
    /// </summary>
    internal enum JpegColorSpace
    {
        Undefined = 0,

        /// <summary>
        /// Color space with 1 component.
        /// </summary>
        Grayscale,

        /// <summary>
        /// Color space with 4 components.
        /// </summary>
        Ycck,

        /// <summary>
        /// Color space with 4 components.
        /// </summary>
        Cmyk,

        /// <summary>
        /// Color space with 3 components.
        /// </summary>
        RGB,

        /// <summary>
        /// Color space with 3 components.
        /// </summary>
        YCbCr
    }
}
