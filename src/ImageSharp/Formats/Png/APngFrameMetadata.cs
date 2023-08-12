// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png.Chunks;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides APng specific metadata information for the image frame.
/// </summary>
public class APngFrameMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="APngFrameMetadata"/> class.
    /// </summary>
    public APngFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="APngFrameMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private APngFrameMetadata(APngFrameMetadata other)
    {
        this.Width = other.Width;
        this.Height = other.Height;
        this.XOffset = other.XOffset;
        this.YOffset = other.YOffset;
        this.DelayNumber = other.DelayNumber;
        this.DelayDenominator = other.DelayDenominator;
        this.DisposeOperation = other.DisposeOperation;
        this.BlendOperation = other.BlendOperation;
    }

    /// <summary>
    /// Gets or sets the width of the following frame
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the following frame
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the X position at which to render the following frame
    /// </summary>
    public int XOffset { get; set; }

    /// <summary>
    /// Gets or sets the Y position at which to render the following frame
    /// </summary>
    public int YOffset { get; set; }

    /// <summary>
    /// Gets or sets the frame delay fraction numerator
    /// </summary>
    public short DelayNumber { get; set; }

    /// <summary>
    /// Gets or sets the frame delay fraction denominator
    /// </summary>
    public short DelayDenominator { get; set; }

    /// <summary>
    /// Gets or sets the type of frame area disposal to be done after rendering this frame
    /// </summary>
    public APngDisposeOperation DisposeOperation { get; set; }

    /// <summary>
    /// Gets or sets the type of frame area rendering for this frame
    /// </summary>
    public APngBlendOperation BlendOperation { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="APngFrameMetadata"/> class.
    /// </summary>
    /// <param name="frameControl">The chunk to create an instance from.</param>
    internal void FromChunk(APngFrameControl frameControl)
    {
        this.Width = frameControl.Width;
        this.Height = frameControl.Height;
        this.XOffset = frameControl.XOffset;
        this.YOffset = frameControl.YOffset;
        this.DelayNumber = frameControl.DelayNumber;
        this.DelayDenominator = frameControl.DelayDenominator;
        this.DisposeOperation = frameControl.DisposeOperation;
        this.BlendOperation = frameControl.BlendOperation;
    }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new APngFrameMetadata(this);
}
