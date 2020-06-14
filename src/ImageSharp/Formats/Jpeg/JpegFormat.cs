// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Jpeg
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the jpeg format.
    /// </summary>
    public sealed class JpegFormat : IImageFormat<JpegMetadata>
    {
        private JpegFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static JpegFormat Instance { get; } = new JpegFormat();

        /// <inheritdoc/>
        public string Name => "JPEG";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/jpeg";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => JpegConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => JpegConstants.FileExtensions;

        /// <inheritdoc/>
        public JpegMetadata CreateDefaultFormatMetadata() => new JpegMetadata();
    }
}