// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Chunks;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides APng specific metadata information for the image frame.
/// </summary>
public class PngFrameMetadata : IDeepCloneable
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
        this.DisposalMethod = other.DisposalMethod;
        this.BlendMethod = other.BlendMethod;
    }

    /// <summary>
    /// Gets or sets the frame delay for animated images.
    /// If not 0, when utilized in Png animation, this field specifies the number of seconds to
    /// wait before continuing with the processing of the Data Stream.
    /// The clock starts ticking immediately after the graphic is rendered.
    /// </summary>
    public Rational FrameDelay { get; set; } = new(0);

    /// <summary>
    /// Gets or sets the type of frame area disposal to be done after rendering this frame
    /// </summary>
    public PngDisposalMethod DisposalMethod { get; set; }

    /// <summary>
    /// Gets or sets the type of frame area rendering for this frame
    /// </summary>
    public PngBlendMethod BlendMethod { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PngFrameMetadata"/> class.
    /// </summary>
    /// <param name="frameControl">The chunk to create an instance from.</param>
    internal void FromChunk(in FrameControl frameControl)
    {
        this.FrameDelay = new Rational(frameControl.DelayNumerator, frameControl.DelayDenominator);
        this.DisposalMethod = frameControl.DisposeOperation;
        this.BlendMethod = frameControl.BlendOperation;
    }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new PngFrameMetadata(this);

    internal static PngFrameMetadata FromAnimatedMetadata(AnimatedImageFrameMetadata metadata)
        => new()
        {
            FrameDelay = new(metadata.Duration.TotalMilliseconds / 1000),
            DisposalMethod = GetMode(metadata.DisposalMode),
            BlendMethod = metadata.BlendMode == FrameBlendMode.Source ? PngBlendMethod.Source : PngBlendMethod.Over,
        };

    private static PngDisposalMethod GetMode(FrameDisposalMode mode) => mode switch
    {
        FrameDisposalMode.RestoreToBackground => PngDisposalMethod.RestoreToBackground,
        FrameDisposalMode.RestoreToPrevious => PngDisposalMethod.RestoreToPrevious,
        FrameDisposalMode.DoNotDispose => PngDisposalMethod.DoNotDispose,
        _ => PngDisposalMethod.DoNotDispose,
    };
}
