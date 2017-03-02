// <copyright file="TiffDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public void Decode<TColor>(Image<TColor> image, Stream stream, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            Guard.NotNull(image, "image");
            Guard.NotNull(stream, "stream");

            using (TiffDecoderCore decoder = new TiffDecoderCore(options))
            {
                decoder.Decode(image, stream, false);
            }
        }
    }
}
