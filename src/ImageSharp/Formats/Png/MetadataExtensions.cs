// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static partial class MetadataExtensions
{
    /// <summary>
    /// Gets the png format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="PngMetadata"/>.</returns>
    public static PngMetadata GetPngMetadata(this ImageMetadata source) => source.GetFormatMetadata(PngFormat.Instance);

    /// <summary>
    /// Gets the aPng format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="PngFrameMetadata"/>.</returns>
    public static PngFrameMetadata GetPngFrameMetadata(this ImageFrameMetadata source) => source.GetFormatMetadata(PngFormat.Instance);

    /// <summary>
    /// Gets the aPng format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>The <see cref="PngFrameMetadata"/>.</returns>
    public static bool TryGetPngFrameMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out PngFrameMetadata? metadata) => source.TryGetFormatMetadata(PngFormat.Instance, out metadata);
}
