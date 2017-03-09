// <copyright file="IPngEncoderOptions.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using Quantizers;

    /// <summary>
    /// Encapsulates the options for the <see cref="PngEncoder"/>.
    /// </summary>
    public interface IPngEncoderOptions : IEncoderOptions
    {
        /// <summary>
        /// Gets the quality of output for images.
        /// </summary>
        int Quality { get; }

        /// <summary>
        /// Gets the png color type
        /// </summary>
        PngColorType PngColorType { get; }

        /// <summary>
        /// Gets the compression level 1-9.
        /// </summary>
        int CompressionLevel { get; }

        /// <summary>
        /// Gets the gamma value, that will be written
        /// the the stream, when the <see cref="WriteGamma"/> property
        /// is set to true.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        float Gamma { get; }

        /// <summary>
        /// Gets quantizer for reducing the color count.
        /// </summary>
        IQuantizer Quantizer { get; }

        /// <summary>
        /// Gets the transparency threshold.
        /// </summary>
        byte Threshold { get; }

        /// <summary>
        /// Gets a value indicating whether this instance should write
        /// gamma information to the stream.
        /// </summary>
        bool WriteGamma { get; }
    }
}
