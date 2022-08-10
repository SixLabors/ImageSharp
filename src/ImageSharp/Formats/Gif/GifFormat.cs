// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    public sealed class GifFormat : IImageFormat<GifMetadata, GifFrameMetadata>
    {
        private GifFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static GifFormat Instance { get; } = new();

        /// <inheritdoc/>
        public string Name => "GIF";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/gif";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => GifConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => GifConstants.FileExtensions;

        /// <inheritdoc/>
        public GifMetadata CreateDefaultFormatMetadata() => new();

        /// <inheritdoc/>
        public GifFrameMetadata CreateDefaultFormatFrameMetadata() => new();
    }
}
