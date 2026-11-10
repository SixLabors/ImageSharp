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
    /// <remarks>
    /// The fraction retains the exact JPEG XL duration. Conversion to shared frame metadata truncates partial
    /// <see cref="TimeSpan"/> ticks and bounds the duration to zero through <see cref="TimeSpan.MaxValue"/>.
    /// Negative and indeterminate durations become zero. Positive infinity becomes <see cref="TimeSpan.MaxValue"/>.
    /// JPEG XL animation encoding requires a finite, nonnegative duration.
    /// </remarks>
    public LongRational FrameDelay { get; set; } = new(0, 1);

    /// <inheritdoc/>
    /// <remarks>
    /// Negative durations become zero. Other durations retain their exact <see cref="TimeSpan"/> tick value.
    /// </remarks>
    public static JxlFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            // TimeSpan already stores integer ticks. Retain that exact fraction instead of approximating double seconds.
            FrameDelay = new LongRational(Math.Max(metadata.Duration.Ticks, 0), TimeSpan.TicksPerSecond).Simplify()
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
    {
        // Scale before dividing so subsecond precision survives. A signed 128-bit intermediate holds
        // any 64-bit numerator times ticks per second, including durations beyond TimeSpan's range.
        // Division truncates partial ticks. Keep the original fraction unchanged and bound only the shared duration.
        LongRational delay = this.FrameDelay;
        Int128 ticks = delay.Denominator == 0
            ? delay.Numerator > 0 ? long.MaxValue : 0
            : ((Int128)delay.Numerator * TimeSpan.TicksPerSecond) / delay.Denominator;

        // Decoder output contains displayed, coalesced frames. Native reference-frame blending stays in the core;
        // callers must not apply it again when these frames are encoded into another animated format.
        return new FormatConnectingFrameMetadata
        {
            BlendMode = FrameBlendMode.Source,
            DisposalMode = FrameDisposalMode.DoNotDispose,
            Duration = TimeSpan.FromTicks((long)Int128.Clamp(ticks, 0, long.MaxValue))
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
