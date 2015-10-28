// <copyright file="GifFormat.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Encapsulates the means to encode and decode gif images.
    /// </summary>
    public class GifFormat : IImageFormat
    {
        /// <inheritdoc/>
        public IImageDecoder Decoder => new GifDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new GifEncoder();
    }
}
