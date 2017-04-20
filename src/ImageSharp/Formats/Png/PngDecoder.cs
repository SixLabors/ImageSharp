// <copyright file="PngDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Encoder for generating an image out of a png encoded stream.
    /// </summary>
    /// <remarks>
    /// At the moment the following features are supported:
    /// <para>
    /// <b>Filters:</b> all filters are supported.
    /// </para>
    /// <para>
    /// <b>Pixel formats:</b>
    /// <list type="bullet">
    ///     <item>RGBA (True color) with alpha (8 bit).</item>
    ///     <item>RGB (True color) without alpha (8 bit).</item>
    ///     <item>Grayscale with alpha (8 bit).</item>
    ///     <item>Grayscale without alpha (8 bit).</item>
    ///     <item>Palette Index with alpha (8 bit).</item>
    ///     <item>Palette Index without alpha (8 bit).</item>
    /// </list>
    /// </para>
    /// </remarks>
    public class PngDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, IDecoderOptions options)

            where TPixel : struct, IPixel<TPixel>
        {
            IPngDecoderOptions pngOptions = PngDecoderOptions.Create(options);

            return this.Decode<TPixel>(configuration, stream, pngOptions);
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <param name="options">The options for the decoder.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, IPngDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            return new PngDecoderCore(options, configuration).Decode<TPixel>(stream);
        }
    }
}
