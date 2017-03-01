// <copyright file="PngEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.IO;

    /// <summary>
    /// Image encoder for writing image data to a stream in png format.
    /// </summary>
    public class PngEncoder : IImageEncoder
    {
        /// <inheritdoc/>
        public void Encode<TColor>(Image<TColor> image, Stream stream, IEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            IPngEncoderOptions pngOptions = PngEncoderOptions.Create(options);

            this.Encode(image, stream, pngOptions);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TColor}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="options">The options for the encoder.</param>
        public void Encode<TColor>(Image<TColor> image, Stream stream, IPngEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            PngEncoderCore encode = new PngEncoderCore(options);
            encode.Encode(image, stream);
        }
    }
}
