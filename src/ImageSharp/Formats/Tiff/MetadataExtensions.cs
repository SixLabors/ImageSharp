// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="ImageMetadata"/> type.
    /// </summary>
    public static partial class MetadataExtensions
    {
        /// <summary>
        /// Gets the tiff format specific metadata for the image.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="TiffMetadata"/>.</returns>
        public static TiffMetadata GetTiffMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(TiffFormat.Instance);

        /// <summary>
        /// Gets the tiff format specific metadata for the image frame.
        /// </summary>
        /// <param name="metadata">The metadata this method extends.</param>
        /// <returns>The <see cref="TiffFrameMetadata"/>.</returns>
        public static TiffFrameMetadata GetTiffMetadata(this ImageFrameMetadata metadata) => metadata.GetFormatMetadata(TiffFormat.Instance);
    }
}
