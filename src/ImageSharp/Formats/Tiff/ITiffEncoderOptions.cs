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
        /// Gets the compression type to use.
        /// </summary>
        TiffEncoderCompression Compression { get; }

        /// <summary>
        /// Gets the encoding mode to use. RGB, RGB with color palette or gray.
        /// </summary>
        TiffEncodingMode Mode { get; }

        /// <summary>
        /// Gets the quantizer for creating a color palette image.
        /// </summary>
        IQuantizer Quantizer { get; }
    }
}
