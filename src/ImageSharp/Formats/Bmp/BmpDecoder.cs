// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Bmp
{
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
    public sealed class BmpDecoder : IImageDecoder, IBmpDecoderOptions, IImageInfoDetector
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, nameof(stream));

            return new BmpDecoderCore(configuration, this).Decode<TPixel>(stream);
        }

        /// <inheritdoc/>
        public IImageInfo Identify(Configuration configuration, Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            return new BmpDecoderCore(configuration, this).Identify(stream);
        }
    }
}
