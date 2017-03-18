// <copyright file="TiffEncoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encoder for writing the data image to a stream in TIFF format.
    /// </summary>
    public class TiffEncoder : IImageEncoder
    {
        /// <inheritdoc/>
        public void Encode<TColor>(Image<TColor> image, Stream stream, IEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            ITiffEncoderOptions tiffOptions = TiffEncoderOptions.Create(options);

            this.Encode(image, stream, tiffOptions);
        }

        /// <summary>
        /// Encodes the image to the specified stream from the <see cref="Image{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="Image{TColor}"/> to encode from.</param>
        /// <param name="stream">The <see cref="Stream"/> to encode the image data to.</param>
        /// <param name="options">The options for the encoder.</param>
        public void Encode<TColor>(Image<TColor> image, Stream stream, ITiffEncoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            throw new NotImplementedException();
        }
    }
}
