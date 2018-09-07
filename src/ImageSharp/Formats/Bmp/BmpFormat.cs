// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bmp format.
    /// </summary>
    internal sealed class BmpFormat : ImageFormatBase<BmpFormat>
    {
        private BmpFormat()
        {
        }

        /// <inheritdoc/>
        public override string Name => "BMP";

        /// <inheritdoc/>
        public override string DefaultMimeType => "image/bmp";

        /// <inheritdoc/>
        public override IEnumerable<string> MimeTypes => BmpConstants.MimeTypes;

        /// <inheritdoc/>
        public override IEnumerable<string> FileExtensions => BmpConstants.FileExtensions;
    }
}