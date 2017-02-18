﻿// <copyright file="GifDecoder.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System;
    using System.IO;

    /// <summary>
    /// Decoder for generating an image out of a gif encoded stream.
    /// </summary>
    public class GifDecoder : IImageDecoder
    {
        /// <inheritdoc/>
        public void Decode<TColor>(Image<TColor> image, Stream stream, IDecoderOptions options)
            where TColor : struct, IPixel<TColor>
        {
            IGifDecoderOptions gifOptions = GifDecoderOptions.Create(options);

            new GifDecoderCore<TColor>(gifOptions).Decode(image, stream);
        }
    }
}
