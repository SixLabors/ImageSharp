// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Configuration options for use during bmp encoding.
    /// </summary>
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

        /// <summary>
        /// Gets the quantizer for reducing the color count for 8-Bit images.
        /// </summary>
        IQuantizer Quantizer { get; }
    }
}