// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Gif
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the gif format.
    /// </summary>
    internal sealed class GifFormat : ImageFormatBase<GifFormat>
    {
        private GifFormat()
        {
        }

        /// <inheritdoc/>
        public override string Name => "GIF";

        /// <inheritdoc/>
        public override string DefaultMimeType => "image/gif";

        /// <inheritdoc/>
        public override IEnumerable<string> MimeTypes => GifConstants.MimeTypes;

        /// <inheritdoc/>
        public override IEnumerable<string> FileExtensions => GifConstants.FileExtensions;
    }
}