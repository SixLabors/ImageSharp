// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Bmp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the bmp format.
    /// </summary>
    public sealed class BmpFormat : IImageFormat<BmpMetadata>
    {
        private BmpFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static BmpFormat Instance { get; } = new BmpFormat();

        /// <inheritdoc/>
        public string Name => "BMP";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/bmp";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => BmpConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => BmpConstants.FileExtensions;

        /// <inheritdoc/>
        public BmpMetadata CreateDefaultFormatMetadata() => new BmpMetadata();
    }
}