// <copyright file="PngEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.IO;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Image encoder for writing image data to a stream in png format.
    /// </summary>
    public class PngEncoder : IImageEncoder
    {
        /// <inheritdoc/>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, IEncoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            IPngEncoderOptions pngOptions = PngEncoderOptions.Create(options);

            this.Encode(image, stream, pngOptions);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TPixel}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="options">The options for the encoder.</param>
        public void Encode<TPixel>(Image<TPixel> image, Stream stream, IPngEncoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            PngEncoderCore encode = new PngEncoderCore(options);
            encode.Encode(image, stream);
        }
    }
}
