// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

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

    /// <summary>
    /// Gets the webp format specific metadata for the image.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the webp metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetWebpMetadata(this ImageMetadata source, [NotNullWhen(true)] out WebpMetadata? metadata)
        => source.TryGetFormatMetadata(WebpFormat.Instance, out metadata);

    /// <summary>
    /// Gets the webp format specific metadata for the image frame.
    /// </summary>
    /// <param name="metadata">The metadata this method extends.</param>
    /// <returns>The <see cref="WebpFrameMetadata"/>.</returns>
    public static WebpFrameMetadata GetWebpMetadata(this ImageFrameMetadata metadata) => metadata.GetFormatMetadata(WebpFormat.Instance);

    /// <summary>
    /// Gets the webp format specific metadata for the image frame.
    /// </summary>
    /// <param name="source">The metadata this method extends.</param>
    /// <param name="metadata">The metadata.</param>
    /// <returns>
    /// <see langword="true"/> if the webp frame metadata exists; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryGetWebpFrameMetadata(this ImageFrameMetadata source, [NotNullWhen(true)] out WebpFrameMetadata? metadata)
        => source.TryGetFormatMetadata(WebpFormat.Instance, out metadata);

    internal static AnimatedImageMetadata ToAnimatedImageMetadata(this WebpMetadata source)
        => new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            RepeatCount = source.RepeatCount,
            BackgroundColor = source.BackgroundColor
        };

    internal static AnimatedImageFrameMetadata ToAnimatedImageFrameMetadata(this WebpFrameMetadata source)
        => new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            Duration = TimeSpan.FromMilliseconds(source.FrameDelay),
            DisposalMode = GetMode(source.DisposalMethod),
            BlendMode = source.BlendMethod == WebpBlendingMethod.Over ? FrameBlendMode.Over : FrameBlendMode.Source,
        };

    private static FrameDisposalMode GetMode(WebpDisposalMethod method) => method switch
    {
        WebpDisposalMethod.RestoreToBackground => FrameDisposalMode.RestoreToBackground,
        WebpDisposalMethod.None => FrameDisposalMode.DoNotDispose,
        _ => FrameDisposalMode.DoNotDispose,
    };
}
