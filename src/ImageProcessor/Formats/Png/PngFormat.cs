// <copyright file="PngFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    /// <summary>
    /// Encapsulates the means to encode and decode png images.
    /// </summary>
    public class PngFormat : IImageFormat
    {
        /// <inheritdoc/>
        public IImageDecoder Decoder => new PngDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new PngEncoder();
    }
}
