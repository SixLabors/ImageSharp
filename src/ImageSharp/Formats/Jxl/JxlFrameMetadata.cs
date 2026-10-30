// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Provides JPEG XL image frame metadata.
/// </summary>
public class JxlFrameMetadata : IFormatFrameMetadata<JxlFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrameMetadata"/> class.
    /// </summary>
    public JxlFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFrameMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private JxlFrameMetadata(JxlFrameMetadata other) => this.FrameDelay = other.FrameDelay;

    /// <summary>
    /// Gets or sets the frame display duration in seconds.
    /// Zero indicates no display delay. The default is zero.
    /// </summary>
    public Rational FrameDelay { get; set; } = new(0);

    /// <inheritdoc/>
    public static JxlFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            FrameDelay = new Rational(metadata.Duration.TotalSeconds)
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
    {
        double seconds = this.FrameDelay.ToDouble();

        // Rational permits a zero denominator. Connecting metadata requires a finite duration;
        // use the same zero delay as the default metadata when the ratio has no finite value.
        if (!double.IsFinite(seconds))
        {
            seconds = 0;
        }

        // Decoder output contains displayed, coalesced frames. Native reference-frame blending stays in the core;
        // callers must not apply it again when these frames are encoded into another animated format.
        return new FormatConnectingFrameMetadata
        {
            BlendMode = FrameBlendMode.Source,
            DisposalMode = FrameDisposalMode.DoNotDispose,
            Duration = TimeSpan.FromSeconds(seconds)
        };
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public JxlFrameMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }
}
