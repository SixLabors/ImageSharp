// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the PBM format.
    /// </summary>
    public sealed class PbmFormat : IImageFormat<PbmMetadata>
    {
        private PbmFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static PbmFormat Instance { get; } = new();

        /// <inheritdoc/>
        public string Name => "PBM";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/x-portable-pixmap";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => PbmConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => PbmConstants.FileExtensions;

        /// <inheritdoc/>
        public PbmMetadata CreateDefaultFormatMetadata() => new();
    }
}
