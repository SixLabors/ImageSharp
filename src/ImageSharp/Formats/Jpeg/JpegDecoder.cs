// <copyright file="JpegDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Image decoder for generating an image out of a jpg stream.
    /// </summary>
    public class JpegDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public Image<TColor> Decode<TColor>(Stream stream, IDecoderOptions options, Configuration configuration)
            where TColor : struct, IPixel<TColor>
        {
            Guard.NotNull(stream, "stream");

            using (JpegDecoderCore decoder = new JpegDecoderCore(options, configuration))
            {
                return decoder.Decode<TColor>(stream);
            }
        }
    }
}
