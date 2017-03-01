// <copyright file="GifEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image encoder for writing image data to a stream in gif format.
    /// </summary>
    public class GifEncoder : IImageEncoder
    {
        /// <inheritdoc/>
        public void Encode<TColor>(Image<TColor> image, Stream stream, IEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            IGifEncoderOptions gifOptions = GifEncoderOptions.Create(options);

            this.Encode(image, stream, gifOptions);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TColor}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="options">The options for the encoder.</param>
        public void Encode<TColor>(Image<TColor> image, Stream stream, IGifEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            GifEncoderCore encoder = new GifEncoderCore(options);
            encoder.Encode(image, stream);
        }
    }
}
