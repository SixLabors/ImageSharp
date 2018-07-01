// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing.Quantization;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// The options available for manipulating the encoder pipeline
    /// </summary>
    internal interface IPngEncoderOptions
    {
        /// <summary>
        /// Gets the number of bits per sample or per palette index (not per pixel).
        /// Not all values are allowed for all <see cref="ColorType"/> values.
        /// </summary>
        PngBitDepth BitDepth { get; }

        /// <summary>
        /// Gets the color type
        /// </summary>
        PngColorType ColorType { get; }

        /// <summary>
        /// Gets the filter method.
        /// </summary>
        PngFilterMethod FilterMethod { get; }

        /// <summary>
        /// Gets the compression level 1-9.
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        int CompressionLevel { get; }

        /// <summary>
        /// Gets the gamma value, that will be written
        /// the the stream, when the <see cref="WriteGamma"/> property
        /// is set to true. The default value is 2.2F.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        float Gamma { get; }

        /// <summary>
        /// Gets  quantizer for reducing the color count.
        /// </summary>
        IQuantizer Quantizer { get; }

        /// <summary>
        /// Gets the transparency threshold.
        /// </summary>
        byte Threshold { get; }

        /// <summary>
        /// Gets a value indicating whether this instance should write
        /// gamma information to the stream. The default value is false.
        /// </summary>
        bool WriteGamma { get; }
    }
}