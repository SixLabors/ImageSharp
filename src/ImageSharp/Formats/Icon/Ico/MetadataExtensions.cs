// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp.Formats.Icon.Ico;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static class MetadataExtensions
{
    /// <summary>
    /// Gets the Ico format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="IcoMetadata"/>.</returns>
    public static IcoMetadata GetIcoMetadata(this ImageMetadata source)
        => source.GetFormatMetadata(IcoFormat.Instance);

    /// <summary>
    /// Gets the Ico format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="IcoFrameMetadata"/>.</returns>
    public static IcoFrameMetadata GetIcoMetadata(this ImageFrameMetadata source)
        => source.GetFormatMetadata(IcoFormat.Instance);

    /// <summary>
    /// Gets the Ico format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified frame,
    /// if found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the Ico frame metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetIcoMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out IcoFrameMetadata? metadata)
        => source.TryGetFormatMetadata(IcoFormat.Instance, out metadata);
}
