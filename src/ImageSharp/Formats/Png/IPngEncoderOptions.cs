// <copyright file="PngEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;
    using System.IO;

    using ImageSharp.PixelFormats;
    using ImageSharp.Quantizers;

    /// <summary>
    /// The options availible for manipulating the encoder pipeline
    /// </summary>
    internal interface IPngEncoderOptions
    {
        /// <summary>
        /// Gets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// </summary>
        bool IgnoreMetadata { get; }

        /// <summary>
        /// Gets the size of the color palette to use. Set to zero to leav png encoding to use pixel data.
        /// </summary>
        int PaletteSize { get; }

        /// <summary>
        /// Gets the png color type
        /// </summary>
        PngColorType PngColorType { get; }

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
