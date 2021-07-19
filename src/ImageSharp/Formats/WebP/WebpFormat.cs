// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Webp
{
    /// <summary>
    /// Registers the image encoders, decoders and mime type detectors for the Webp format
    /// </summary>
    public sealed class WebpFormat : IImageFormat<WebpMetadata>
    {
        private WebpFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static WebpFormat Instance { get; } = new WebpFormat();

        /// <inheritdoc/>
        public string Name => "WebP";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/webp";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => WebpConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => WebpConstants.FileExtensions;

        /// <inheritdoc/>
        public WebpMetadata CreateDefaultFormatMetadata() => new WebpMetadata();
    }
}
