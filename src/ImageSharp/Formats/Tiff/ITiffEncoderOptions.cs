// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Encapsulates the options for the <see cref="TiffEncoder"/>.
    /// </summary>
    internal interface ITiffEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per pixel.
        /// </summary>
        TiffBitsPerPixel? BitsPerPixel { get; }

        /// <summary>
        /// Gets the compression type to use.
        /// </summary>
        TiffEncoderCompression Compression { get; }

        /// <summary>
        /// Gets a value indicating whether to use a color palette.
        /// </summary>
        bool UseColorPalette { get; }

        /// <summary>
        /// Gets the quantizer for creating a color palette image.
        /// </summary>
        IQuantizer Quantizer { get; }
    }
}
