// <copyright file="PngFormat.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    using System.Collections.Generic;

    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    internal sealed class PngFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Name => "PNG";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/png";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => PngConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => PngConstants.FileExtensions;
    }
}