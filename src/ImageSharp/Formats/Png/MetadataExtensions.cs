// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats;
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
    /// Gets the png format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the png metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetPngMetadata(this ImageMetadata source, [NotNullWhen(true)] out PngMetadata? metadata)
        => source.TryGetFormatMetadata(PngFormat.Instance, out metadata);

    /// <summary>
    /// Gets the png format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <returns>The <see cref="PngFrameMetadata"/>.</returns>
    public static PngFrameMetadata GetPngMetadata(this ImageFrameMetadata source) => source.GetFormatMetadata(PngFormat.Instance);

    /// <summary>
    /// Gets the png format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the png frame metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetPngMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out PngFrameMetadata? metadata)
        => source.TryGetFormatMetadata(PngFormat.Instance, out metadata);

    internal static AnimatedImageMetadata ToAnimatedImageMetadata(this PngMetadata source)
        => new()
        {
            ColorTable = source.ColorTable,
            ColorTableMode = FrameColorTableMode.Global,
            RepeatCount = (ushort)Numerics.Clamp(source.RepeatCount, 0, ushort.MaxValue),
        };

    internal static AnimatedImageFrameMetadata ToAnimatedImageFrameMetadata(this PngFrameMetadata source)
    {
        double delay = source.FrameDelay.ToDouble();
        if (double.IsNaN(delay))
        {
            delay = 0;
        }

        return new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            Duration = TimeSpan.FromMilliseconds(delay * 1000),
            DisposalMode = GetMode(source.DisposalMethod),
            BlendMode = source.BlendMethod == PngBlendMethod.Source ? FrameBlendMode.Source : FrameBlendMode.Over,
        };
    }

    private static FrameDisposalMode GetMode(PngDisposalMethod method) => method switch
    {
        PngDisposalMethod.DoNotDispose => FrameDisposalMode.DoNotDispose,
        PngDisposalMethod.RestoreToBackground => FrameDisposalMode.RestoreToBackground,
        PngDisposalMethod.RestoreToPrevious => FrameDisposalMode.RestoreToPrevious,
        _ => FrameDisposalMode.Unspecified,
    };
}
