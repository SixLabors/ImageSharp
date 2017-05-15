// <copyright file="TiffDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.IO;
    using ImageSharp.PixelFormats;

    /// <summary>
    /// Image decoder for generating an image out of a TIFF stream.
    /// </summary>
    public class TiffDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            using (TiffDecoderCore decoder = new TiffDecoderCore(options, configuration))
            {
                return decoder.Decode<TPixel>(stream);
            }
        }
    }
}
