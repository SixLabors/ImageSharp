// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Configuration options for use during bmp encoding
    /// </summary>
    /// <remarks>The encoder can currently only write 24-bit and 32-bit rgb images to streams.</remarks>
    internal interface IBmpEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        BmpBitsPerPixel? BitsPerPixel { get; }

        /// <summary>
        /// Gets a value indicating whether the encoder should support transparency.
        /// </summary>
        bool SupportTransparency { get; }
    }
}