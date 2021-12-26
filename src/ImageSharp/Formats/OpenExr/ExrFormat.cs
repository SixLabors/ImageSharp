// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.Formats.Bmp;

namespace SixLabors.ImageSharp.Formats.OpenExr
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the OpenExr format.
    /// </summary>
    public sealed class ExrFormat : IImageFormat<ExrMetadata>
    {
        private ExrFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static ExrFormat Instance { get; } = new();

        /// <inheritdoc/>
        public string Name => "EXR";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/x-exr";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => ExrConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => ExrConstants.FileExtensions;

        /// <inheritdoc/>
        public ExrMetadata CreateDefaultFormatMetadata() => new();
    }
}
