// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Configuration options for use during bmp encoding
    /// </summary>
    /// <remarks>The encoder can currently only write 16-bit, 24-bit and 32-bit rgb images to streams.</remarks>
    internal interface IBmpEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        BmpBitsPerPixel? BitsPerPixel { get; }

        /// <summary>
        /// Gets a value indicating whether the encoder should support transparency.
        /// Note: Transparency support only works together with 32 bits per pixel. This option will
        /// change the default behavior of the encoder of writing a bitmap version 3 info header with no compression.
        /// Instead a bitmap version 4 info header will be written with the BITFIELDS compression.
        /// </summary>
        bool SupportTransparency { get; }
    }
}