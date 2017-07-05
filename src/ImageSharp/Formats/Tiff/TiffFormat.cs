// <copyright file="TiffFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;
    using ImageSharp.Formats.Tiff;

    /// <summary>
    /// Encapsulates the means to encode and decode Tiff images.
    /// </summary>
    public class TiffFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Name => "TIFF";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/tiff";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => TiffConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => TiffConstants.FileExtensions;
    }
}
