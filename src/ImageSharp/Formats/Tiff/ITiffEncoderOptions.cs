// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
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
        TiffCompression? Compression { get; }

        /// <summary>
        /// Gets the compression level 1-9 for the deflate compression mode.
        /// <remarks>Defaults to <see cref="DeflateCompressionLevel.DefaultCompression"/>.</remarks>
        /// </summary>
        DeflateCompressionLevel? CompressionLevel { get; }

        /// <summary>
        /// Gets the PhotometricInterpretation to use. Possible options are RGB, RGB with a color palette, gray or BiColor.
        /// If no PhotometricInterpretation is specified or it is unsupported by the encoder, RGB will be used.
        /// </summary>
        TiffPhotometricInterpretation? PhotometricInterpretation { get; }

        /// <summary>
        /// Gets a value indicating which horizontal prediction to use. This can improve the compression ratio with deflate or lzw compression.
        /// </summary>
        TiffPredictor? HorizontalPredictor { get; }

        /// <summary>
        /// Gets the quantizer for creating a color palette image.
        /// </summary>
        IQuantizer Quantizer { get; }
    }
}
