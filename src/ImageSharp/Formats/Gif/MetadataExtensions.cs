// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="ImageMetadata"/> type.
/// </summary>
public static partial class MetadataExtensions
{
    /// <summary>
    /// Gets the gif format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="GifMetadata"/>.</returns>
    public static GifMetadata GetGifMetadata(this ImageMetadata source) => source.GetFormatMetadata(GifFormat.Instance);

    /// <summary>
    /// Gets the gif format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="GifFrameMetadata"/>.</returns>
    public static GifFrameMetadata GetGifMetadata(this ImageFrameMetadata source) => source.GetFormatMetadata(GifFormat.Instance);

    /// <summary>
    /// Gets the gif format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified frame,
    /// if found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the gif frame metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetGifMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out GifFrameMetadata? metadata) => source.TryGetFormatMetadata(GifFormat.Instance, out metadata);
}
