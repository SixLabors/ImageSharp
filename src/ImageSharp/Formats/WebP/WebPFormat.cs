// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

namespace SixLabors.ImageSharp.Formats.Experimental.WebP
{
    /// <summary>
    /// EXPERIMENTAL:
    /// Registers the image encoders, decoders and mime type detectors for the WebP format
    /// </summary>
    public sealed class WebPFormat : IImageFormat<WebPMetadata>
    {
        private WebPFormat()
        {
        }

        /// <summary>
        /// Gets the current instance.
        /// </summary>
        public static WebPFormat Instance { get; } = new WebPFormat();

        /// <inheritdoc/>
        public string Name => "WebP";

        /// <inheritdoc/>
        public string DefaultMimeType => "image/webp";

        /// <inheritdoc/>
        public IEnumerable<string> MimeTypes => WebPConstants.MimeTypes;

        /// <inheritdoc/>
        public IEnumerable<string> FileExtensions => WebPConstants.FileExtensions;

        /// <inheritdoc/>
        public WebPMetadata CreateDefaultFormatMetadata() => new WebPMetadata();
    }
}
