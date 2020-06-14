// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Tga
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the tga format.
    /// </summary>
    public sealed class TgaFormat : IImageFormat<TgaMetadata>
    {
        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static TgaFormat Instance { get; } = new TgaFormat();

        /// <inheritdoc/>
        public string Name => "TGA";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/tga";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => TgaConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => TgaConstants.FileExtensions;

        /// <inheritdoc/>
        public TgaMetadata CreateDefaultFormatMetadata() => new TgaMetadata();
    }
}
