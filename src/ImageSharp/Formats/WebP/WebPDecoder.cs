// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.WebP
{
    /// <summary>
    /// Image decoder for generating an image out of a webp stream.
    /// </summary>
    public sealed class WebPDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream)
            where TPixel : struct, IPixel<TPixel>
        {
            // TODO: [brianpopow] parse chunks and decide which decoder (subclass of WebPDecoderCoreBase) to use
            throw new System.NotImplementedException();
        }

        /// <inheritdoc/>
        public Image Decode(Configuration configuration, Stream stream)
        {
            throw new System.NotImplementedException();
        }
    }
}
