// <copyright file="PngDecoder.cs" company="James Jackson-South">
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
        /// Gets or sets a value indicating whether the metadata should be ignored when the image is being decoded.
        /// </summary>
        public bool IgnoreMetadata { get; set; }

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => PngConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => PngConstants.FileExtensions;

        /// <inheritdoc/>
        public int HeaderSize => 8;

        /// <summary>
        /// Gets or sets the encoding that should be used when reading text chunks.
        /// </summary>
        public Encoding TextEncoding { get; set; } = PngConstants.DefaultEncoding;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(Span<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x89 &&
                   header[1] == 0x50 && // P
                   header[2] == 0x4E && // N
                   header[3] == 0x47 && // G
                   header[4] == 0x0D && // CR
                   header[5] == 0x0A && // LF
                   header[6] == 0x1A && // EOF
                   header[7] == 0x0A;   // LF
        }

        /// <summary>
        /// Decodes the image from the specified stream to the <see cref="ImageBase{TPixel}"/>.
        /// </summary>
        /// <typeparam name="TPixel">The pixel format.</typeparam>
        /// <param name="configuration">The configuration for the image.</param>
        /// <param name="stream">The <see cref="Stream"/> containing image data.</param>
        /// <returns>The decoded image.</returns>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            var decoder = new PngDecoderCore(configuration, this.TextEncoding);
            decoder.IgnoreMetadata = this.IgnoreMetadata;
            return decoder.Decode<TPixel>(stream);
        }
    }
}
