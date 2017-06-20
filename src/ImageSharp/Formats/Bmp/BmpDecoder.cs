// <copyright file="BmpDecoder.cs" company="James Jackson-South">
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
    /// Image decoder for generating an image out of a Windows bitmap stream.
    /// </summary>
    /// <remarks>
    /// Does not support the following formats at the moment:
    /// <list type="bullet">
    ///    <item>JPG</item>
    ///    <item>PNG</item>
    ///    <item>RLE4</item>
    ///    <item>RLE8</item>
    ///    <item>BitFields</item>
    /// </list>
    /// Formats will be supported in a later releases. We advise always
    /// to use only 24 Bit Windows bitmaps.
    /// </remarks>
    public class BmpDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => BmpConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => BmpConstants.FileExtensions;

        /// <inheritdoc/>
        public int HeaderSize => 2;

        /// <inheritdoc/>
        public bool IsSupportedFileFormat(Span<byte> header)
        {
            return header.Length >= this.HeaderSize &&
                   header[0] == 0x42 && // B
                   header[1] == 0x4D;   // M
        }

        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)

            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            return new BmpDecoderCore(configuration).Decode<TPixel>(stream);
        }
    }
}
