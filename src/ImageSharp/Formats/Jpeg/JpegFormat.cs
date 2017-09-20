// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    using System.Collections.Generic;
    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort;

    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the jpeg format.
    /// </summary>
    internal sealed class JpegFormat : IImageFormat
    {
        /// <inheritdoc/>
        public string Name => "JPEG";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/jpeg";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => OrigJpegConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => OrigJpegConstants.FileExtensions;
    }
}