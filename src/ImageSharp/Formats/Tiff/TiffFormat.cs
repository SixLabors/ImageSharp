// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Encapsulates the means to encode and decode Tiff images.
    /// </summary>
    public class TiffFormat : IImageFormat<TiffMetaData>
    {
        private TiffFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static TiffFormat Instance { get; } = new TiffFormat();

        /// <inheritdoc/>
        public string Name => "TIFF";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/tiff";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => TiffConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => TiffConstants.FileExtensions;

        /// <inheritdoc/>
        public TiffMetaData CreateDefaultFormatMetadata() => new TiffMetaData();
    }
}
