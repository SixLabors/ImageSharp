// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Provides HEIF-specific metadata for an image frame.
/// </summary>
public class HeifFrameMetadata : IFormatFrameMetadata<HeifFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifFrameMetadata"/> class.
    /// </summary>
    public HeifFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifFrameMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private HeifFrameMetadata(HeifFrameMetadata other) => this.FrameDelay = other.FrameDelay;

    /// <summary>
    /// Gets or sets the frame display duration in seconds. The numerator contains duration units and the denominator
    /// contains units per second. Zero indicates that no explicit duration is available.
    /// </summary>
    public Rational FrameDelay { get; set; } = new(0);

    /// <inheritdoc/>
    public static HeifFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            FrameDelay = new Rational(metadata.Duration.TotalSeconds)
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
    {
        double seconds = this.FrameDelay.ToDouble();

        // Rational permits a zero denominator for metadata roundtripping. ImageSharp's connecting metadata cannot
        // represent an infinite duration, so preserve it as the same unspecified zero duration used by other formats.
        if (!double.IsFinite(seconds))
        {
            seconds = 0;
        }

        return new FormatConnectingFrameMetadata
        {
            BlendMode = FrameBlendMode.Source,
            DisposalMode = FrameDisposalMode.DoNotDispose,
            Duration = TimeSpan.FromSeconds(seconds)
        };
    }

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public HeifFrameMetadata DeepClone() => new(this);
}
