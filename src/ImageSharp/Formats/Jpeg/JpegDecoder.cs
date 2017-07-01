// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public sealed class JpegDecoder : IImageDecoder, IJpegDecoderOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            using (JpegDecoderCore decoder = new JpegDecoderCore(configuration, this))
            {
                return decoder.Decode<TPixel>(stream);
            }
        }
    }
}
