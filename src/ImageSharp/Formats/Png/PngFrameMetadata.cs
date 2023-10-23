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
        this.DelayNumerator = other.DelayNumerator;
        this.DelayDenominator = other.DelayDenominator;
        this.DisposalMethod = other.DisposalMethod;
        this.BlendMethod = other.BlendMethod;
    }

    /// <summary>
    /// Gets or sets the frame delay fraction numerator
    /// </summary>
    public ushort DelayNumerator { get; set; }

    /// <summary>
    /// Gets or sets the frame delay fraction denominator
    /// </summary>
    public ushort DelayDenominator { get; set; }

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
        this.DelayNumerator = frameControl.DelayNumerator;
        this.DelayDenominator = frameControl.DelayDenominator;
        this.DisposalMethod = frameControl.DisposeOperation;
        this.BlendMethod = frameControl.BlendOperation;
    }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new PngFrameMetadata(this);
}
