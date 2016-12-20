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
        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase{TColor}"/>.
        /// </summary>
        /// <typeparam name="TColor">The pixel format.</typeparam>
        /// <param name="image">The <see cref="ImageBase{TColor}"/> to decode to.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        public void Decode<TColor>(Image<TColor> image, Stream stream)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            new PngDecoderCore().Decode(image, stream);
        }
    }
}
