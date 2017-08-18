// <copyright file="GifDecoder.cs" company="James Jackson-South">
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

    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    public sealed class GifDecoder : IImageDecoder, IGifDecoderOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; } = false;

        /// <summary>
        /// Gets or sets the encoding that should be used when reading comments.
        /// </summary>
        public Encoding TextEncoding { get; set; } = GifConstants.DefaultEncoding;

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new GifDecoderCore<TPixel>(configuration, this);
            return decoder.Decode(stream);
        }

        /// <inheritdoc/>
        public PixelTypeInfo DetectPixelType(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, "stream");

            byte[] buffer = new byte[1];
            stream.Skip(10); // Skip the identifier and size
            stream.Read(buffer, 0, 1); // Skip the identifier and size
            int bitsPerPixel = (buffer[0] & 0x07) + 1;  // The lowest 3 bits represent the bit depth minus 1
            return new PixelTypeInfo(bitsPerPixel);
        }
    }
}
