// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="ImageMetadata"/> type.
    /// </summary>
    public static partial class MetadataExtensions
    {
        /// <summary>
        /// Gets the webp format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="WebpMetadata"/>.</returns>
        public static WebpMetadata GetWebpMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(WebpFormat.Instance);
    }
}
