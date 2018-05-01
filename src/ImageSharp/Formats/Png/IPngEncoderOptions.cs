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
        /// Gets the png color type
        /// </summary>
        PngColorType PngColorType { get; }

        /// <summary>
        /// Gets the png filter method.
        /// </summary>
        PngFilterMethod PngFilterMethod { get; }

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