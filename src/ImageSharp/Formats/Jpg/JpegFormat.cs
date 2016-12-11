// <copyright file="JpegFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Encapsulates the means to encode and decode jpeg images.
    /// </summary>
    public class JpegFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string MimeType => "image/jpeg";

        /// <inheritdoc/>
        public string Extension => "jpg";

        /// <inheritdoc/>
        public IEnumerable<string> SupportedExtensions => new string[] { "jpg", "jpeg", "jfif" };

        /// <inheritdoc/>
        public IImageDecoder Decoder => new JpegDecoder();

        /// <inheritdoc/>
        public IImageEncoder Encoder => new JpegEncoder();
    }
}
