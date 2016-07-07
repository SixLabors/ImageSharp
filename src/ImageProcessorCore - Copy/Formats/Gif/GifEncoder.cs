// <copyright file="GifEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Formats
{
    using System;
    using System.IO;

    using ImageProcessorCore.Quantizers;

    /// <summary>
    /// Image encoder for writing image data to a stream in gif format.
    /// </summary>
    public class GifEncoder : IImageEncoder
    {
        /// <summary>
        /// Gets or sets the quality of output for images.
        /// </summary>
        /// <remarks>For gifs the value ranges from 1 to 256.</remarks>
        public int Quality { get; set; }

        /// <summary>
        /// Gets or sets the transparency threshold.
        /// </summary>
        public byte Threshold { get; set; } = 128;

        /// <summary>
        /// The quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <inheritdoc/>
        public string Extension => "gif";

        /// <inheritdoc/>
        public string MimeType => "image/gif";

        /// <inheritdoc/>
        public bool IsSupportedFileExtension(string extension)
        {
            Guard.NotNullOrEmpty(extension, nameof(extension));

            extension = extension.StartsWith(".") ? extension.Substring(1) : extension;
            return extension.Equals(this.Extension, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public void Encode(ImageBase image, Stream stream)
        {
            GifEncoderCore encoder = new GifEncoderCore
            {
                Quality = this.Quality,
                Quantizer = this.Quantizer,
                Threshold = this.Threshold
            };

            encoder.Encode(image, stream);
        }
    }
}
