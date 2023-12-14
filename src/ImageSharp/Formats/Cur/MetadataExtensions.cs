// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static class MetadataExtensions
{
    /// <summary>
    /// Gets the Icon format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="CurMetadata"/>.</returns>
    public static CurMetadata GetCurMetadata(this ImageMetadata source)
        => source.GetFormatMetadata(CurFormat.Instance);

    /// <summary>
    /// Gets the Icon format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="CurFrameMetadata"/>.</returns>
    public static CurFrameMetadata GetCurMetadata(this ImageFrameMetadata source)
        => source.GetFormatMetadata(CurFormat.Instance);

    /// <summary>
    /// Gets the Icon format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified frame,
    /// if found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the Icon frame metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetCurMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out CurFrameMetadata? metadata)
        => source.TryGetFormatMetadata(CurFormat.Instance, out metadata);
}
