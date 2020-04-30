// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public sealed class PngFormat : IImageFormat<PngMetadata>
    {
        private PngFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static PngFormat Instance { get; } = new PngFormat();

        /// <inheritdoc/>
        public string Name => "PNG";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/png";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => PngConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => PngConstants.FileExtensions;

        /// <inheritdoc/>
        public PngMetadata CreateDefaultFormatMetadata() => new PngMetadata();
    }
}