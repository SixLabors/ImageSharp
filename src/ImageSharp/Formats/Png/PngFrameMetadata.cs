// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides APng specific metadata information for the image frame.
/// </summary>
public class PngFrameMetadata : IFormatFrameMetadata<PngFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PngFrameMetadata"/> class.
    /// </summary>
    public PngFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PngFrameMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private PngFrameMetadata(PngFrameMetadata other)
    {
        this.FrameDelay = other.FrameDelay;
        this.DisposalMode = other.DisposalMode;
        this.BlendMode = other.BlendMode;
    }

    /// <summary>
    /// Gets or sets the frame delay for animated images.
    /// If not 0, when utilized in Png animation, this field specifies the number of hundredths (1/100) of a second to
    /// wait before continuing with the processing of the Data Stream.
    /// The clock starts ticking immediately after the graphic is rendered.
    /// </summary>
    public Rational FrameDelay { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the type of frame area disposal to be done after rendering this frame
    /// </summary>
    public FrameDisposalMode DisposalMode { get; set; }

    /// <summary>
    /// Gets or sets the type of frame area rendering for this frame
    /// </summary>
    public FrameBlendMode BlendMode { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PngFrameMetadata"/> class.
    /// </summary>
    /// <param name="frameControl">The chunk to create an instance from.</param>
    internal void FromChunk(in FrameControl frameControl)
    {
        this.FrameDelay = new Rational(frameControl.DelayNumerator, frameControl.DelayDenominator);
        this.DisposalMode = frameControl.DisposalMode;
        this.BlendMode = frameControl.BlendMode;
    }

    /// <inheritdoc/>
    public static PngFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            FrameDelay = new(metadata.Duration.TotalMilliseconds / 1000),
            DisposalMode = GetMode(metadata.DisposalMode),
            BlendMode = metadata.BlendMode,
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
    {
        double delay = this.FrameDelay.ToDouble();
        if (double.IsNaN(delay))
        {
            delay = 0;
        }

        return new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            Duration = TimeSpan.FromMilliseconds(delay * 1000),
            DisposalMode = this.DisposalMode,
            BlendMode = this.BlendMode,
        };
    }

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public PngFrameMetadata DeepClone() => new(this);

    private static FrameDisposalMode GetMode(FrameDisposalMode mode) => mode switch
    {
        FrameDisposalMode.RestoreToBackground => FrameDisposalMode.RestoreToBackground,
        FrameDisposalMode.RestoreToPrevious => FrameDisposalMode.RestoreToPrevious,
        FrameDisposalMode.DoNotDispose => FrameDisposalMode.DoNotDispose,
        _ => FrameDisposalMode.DoNotDispose,
    };
}
