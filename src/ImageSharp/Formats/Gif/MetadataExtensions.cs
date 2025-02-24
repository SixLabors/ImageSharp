// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats;
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
    public static GifMetadata GetGifMetadata(this ImageMetadata source)
        => source.GetFormatMetadata(GifFormat.Instance);

    /// <summary>
    /// Gets the gif format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">
    /// When this method returns, contains the metadata associated with the specified image,
    /// if found; otherwise, the default value for the type of the metadata parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the gif metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetGifMetadata(this ImageMetadata source, [NotNullWhen(true)] out GifMetadata? metadata)
        => source.TryGetFormatMetadata(GifFormat.Instance, out metadata);

    /// <summary>
    /// Gets the gif format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="GifFrameMetadata"/>.</returns>
    public static GifFrameMetadata GetGifMetadata(this ImageFrameMetadata source)
        => source.GetFormatMetadata(GifFormat.Instance);

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
    public static bool TryGetGifMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out GifFrameMetadata? metadata)
        => source.TryGetFormatMetadata(GifFormat.Instance, out metadata);

    internal static AnimatedImageMetadata ToAnimatedImageMetadata(this GifMetadata source)
    {
        bool global = source.ColorTableMode == GifColorTableMode.Global;
        Color color = global && source.GlobalColorTable.HasValue && source.GlobalColorTable.Value.Span.Length > source.BackgroundColorIndex
            ? source.GlobalColorTable.Value.Span[source.BackgroundColorIndex]
            : Color.Transparent;

        return new()
        {
            ColorTableMode = source.ColorTableMode == GifColorTableMode.Global ? FrameColorTableMode.Global : FrameColorTableMode.Local,
            ColorTable = global ? source.GlobalColorTable : null,
            RepeatCount = source.RepeatCount,
            BackgroundColor = color,
        };
    }

    internal static AnimatedImageFrameMetadata ToAnimatedImageFrameMetadata(this GifFrameMetadata source)
    {
        // For most scenarios we would consider the blend method to be 'Over' however if a frame has a disposal method of 'RestoreToBackground' or
        // has a local palette with 256 colors and is not transparent we should use 'Source'.
        bool blendSource = source.DisposalMethod == GifDisposalMethod.RestoreToBackground || (source.LocalColorTable?.Length == 256 && !source.HasTransparency);

        // If the color table is global and frame has no transparency. Consider it 'Source' also.
        blendSource |= source.ColorTableMode == GifColorTableMode.Global && !source.HasTransparency;

        return new()
        {
            ColorTable = source.LocalColorTable,
            ColorTableMode = source.ColorTableMode == GifColorTableMode.Global ? FrameColorTableMode.Global : FrameColorTableMode.Local,
            Duration = TimeSpan.FromMilliseconds(source.FrameDelay * 10),
            DisposalMode = GetMode(source.DisposalMethod),
            BlendMode = blendSource ? FrameBlendMode.Source : FrameBlendMode.Over,
        };
    }

    private static FrameDisposalMode GetMode(GifDisposalMethod method) => method switch
    {
        GifDisposalMethod.NotDispose => FrameDisposalMode.DoNotDispose,
        GifDisposalMethod.RestoreToBackground => FrameDisposalMode.RestoreToBackground,
        GifDisposalMethod.RestoreToPrevious => FrameDisposalMode.RestoreToPrevious,
        _ => FrameDisposalMode.Unspecified,
    };
}
