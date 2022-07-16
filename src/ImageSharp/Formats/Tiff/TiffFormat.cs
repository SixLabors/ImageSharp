// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace SixLabors.ImageSharp.Formats.Tiff
{
    /// <summary>
    /// Encapsulates the means to encode and decode Tiff images.
    /// </summary>
    public sealed class TiffFormat : IImageFormat<TiffMetadata, TiffFrameMetadata>
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
        public TiffMetadata CreateDefaultFormatMetadata() => new TiffMetadata();

        /// <inheritdoc/>
        public TiffFrameMetadata CreateDefaultFormatFrameMetadata() => new TiffFrameMetadata();
    }
}
