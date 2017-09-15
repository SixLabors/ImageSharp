// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bmp format.
    /// </summary>
    internal sealed class BmpFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Name => "BMP";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/bmp";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => BmpConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => BmpConstants.FileExtensions;
    }
}