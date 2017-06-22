// <copyright file="GifEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using ImageSharp.PixelFormats;
    using ImageSharp.Quantizers;

    /// <summary>
    /// Image encoder for writing image data to a stream in gif format.
    /// </summary>
    public class GifEncoder : IImageEncoder
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being encoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the encoding that should be used when writing comments.
        /// </summary>
        public Encoding TextEncoding { get; set; } = GifConstants.DefaultEncoding;

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
        /// Gets or sets the quantizer for reducing the color count.
        /// </summary>
        public IQuantizer Quantizer { get; set; }

        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            GifEncoderCore encoder = new GifEncoderCore(this.TextEncoding);
            encoder.Quantizer = this.Quantizer;
            encoder.Threshold = this.Threshold;
            encoder.Quality = this.Quality;
            encoder.IgnoreMetadata = this.IgnoreMetadata;
            encoder.Encode(image, stream);
        }
    }
}
