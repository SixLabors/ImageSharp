// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    internal sealed class PngFormat : ImageFormatBase<PngFormat>
    {
        private PngFormat()
        {
        }

        /// <inheritdoc/>
        public override string Name => "PNG";

        /// <inheritdoc/>
        public override string DefaultMimeType => "image/png";

        /// <inheritdoc/>
        public override IEnumerable<string> MimeTypes => PngConstants.MimeTypes;

        /// <inheritdoc/>
        public override IEnumerable<string> FileExtensions => PngConstants.FileExtensions;
    }
}