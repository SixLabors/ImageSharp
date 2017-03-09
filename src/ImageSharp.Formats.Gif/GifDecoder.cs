// <copyright file="GifDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    public class GifDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public void Decode<TColor>(Image<TColor> image, Stream stream, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            IGifDecoderOptions gifOptions = GifDecoderOptions.Create(options);

            this.Decode(image, stream, gifOptions);
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        public void Decode<TColor>(Image<TColor> image, Stream stream, IGifDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            new GifDecoderCore<TColor>(options).Decode(image, stream);
        }
    }
}
