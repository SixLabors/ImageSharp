// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static partial class MetadataExtensions
{
    /// <summary>
    /// Gets the HEIF format specific metadata for the image.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The <see cref="HeifMetadata"/>.</returns>
    public static HeifMetadata GetHeifMetadata(this ImageMetadata metadata) => metadata.GetFormatMetadata(HeifFormat.Instance);

    /// <summary>
    /// Creates a new cloned instance of the HEIF metadata associated with the image.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The cloned <see cref="HeifMetadata"/>.</returns>
    public static HeifMetadata CloneHeifMetadata(this ImageMetadata metadata) => metadata.CloneFormatMetadata(HeifFormat.Instance);

    /// <summary>
    /// Gets the HEIF format-specific metadata for the image frame. If none is present, metadata is converted from
    /// the decoded format or a default instance is created and associated with the frame.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The <see cref="HeifFrameMetadata"/>.</returns>
    public static HeifFrameMetadata GetHeifMetadata(this ImageFrameMetadata metadata) => metadata.GetFormatMetadata(HeifFormat.Instance);

    /// <summary>
    /// Creates a new cloned instance of the HEIF metadata associated with the image frame.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The cloned <see cref="HeifFrameMetadata"/>.</returns>
    public static HeifFrameMetadata CloneHeifMetadata(this ImageFrameMetadata metadata) => metadata.CloneFormatMetadata(HeifFormat.Instance);
}
