// <copyright file="PngEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    using ImageProcessorCore.Quantizers;

    /// <summary>
    /// Image encoder for writing image data to a stream in png format.
    /// </summary>
    public class PngEncoder : IImageEncoder
    {
        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the png color type
        /// </summary>
        public PngColorType PngColorType { get; set; } = PngColorType.RgbWithAlpha;

        /// <inheritdoc/>
        public string MimeType => "image/png";

        /// <inheritdoc/>
        public string Extension => "png";

        /// <summary>
        /// Gets or sets the compression level 1-9. 
        /// <remarks>Defaults to 6.</remarks>
        /// </summary>
        public int CompressionLevel { get; set; } = 6;

        /// <summary>
        /// Gets or sets the gamma value, that will be written
        /// the the stream, when the <see cref="WriteGamma"/> property
        /// is set to true. The default value is 2.2F.
        /// </summary>
        /// <value>The gamma value of the image.</value>
        public float Gamma { get; set; } = 2.2F;

        /// <summary>
        /// Gets or sets quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = 0;

        /// <summary>
        /// Gets or sets a value indicating whether this instance should write
        /// gamma information to the stream. The default value is false.
        /// </summary>
        public bool WriteGamma { get; set; }

        /// <inheritdoc/>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;

            return extension.Equals(this.Extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public void Encode<TColor, TPacked>(Image<TColor, TPacked> image, Stream stream)
            where TColor : IPackedVector<TPacked>
            where TPacked : struct
        {
            PngEncoderCore encoder = new PngEncoderCore
            {
                CompressionLevel = this.CompressionLevel,
                Gamma = this.Gamma,
                Quality = this.Quality,
                PngColorType = this.PngColorType,
                Quantizer = this.Quantizer,
                WriteGamma = this.WriteGamma,
                Threshold = this.Threshold
            };

            encoder.Encode(image, stream);
        }
    }
}
