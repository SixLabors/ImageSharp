// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    using ImageSharp.PixelFormats;

    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public class JpegDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public Image<TPixel> Decode<TPixel>(Configuration configuration, Stream stream, IDecoderOptions options)
            where TPixel : struct, IPixel<TPixel>
        {
            Guard.NotNull(stream, "stream");

            // using (JpegDecoderCore decoder = new JpegDecoderCore(options, configuration))
            // {
            //    return decoder.Decode<TPixel>(stream);
            // }
            var decoder = new Jpeg.Port.JpegDecoderCore(options, configuration);
            return decoder.Decode<TPixel>(stream);
        }
    }
}
