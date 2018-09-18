// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Png
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the png format.
    /// </summary>
    public sealed class PngFormat : IImageFormat<PngMetaData>
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
        public PngMetaData CreateDefaultFormatMetaData() => new PngMetaData();
    }
}