// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heic;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static partial class MetadataExtensions
{
    /// <summary>
    /// Gets the pbm format specific metadata for the image.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The <see cref="HeicMetadata"/>.</returns>
    public static HeicMetadata GetHeicMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(HeicFormat.Instance);
}
