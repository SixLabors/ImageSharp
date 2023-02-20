// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Provides Tiff specific metadata information for the image.
/// </summary>
public class TiffMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
    /// </summary>
    public TiffMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private TiffMetadata(TiffMetadata other)
    {
        this.ByteOrder = other.ByteOrder;
        this.FormatType = other.FormatType;

        var frames = new List<TiffFrameMetadata>(other.Frames.Count);
        foreach (var otherFrame in other.Frames)
        {
            var frame = (TiffFrameMetadata)otherFrame.DeepClone();
            frames.Add(frame);
        }

        this.Frames = frames;
    }

    /// <summary>
    /// Gets or sets the byte order.
    /// </summary>
    public ByteOrder ByteOrder { get; set; }

    /// <summary>
    /// Gets or sets the format type.
    /// </summary>
    public TiffFormatType FormatType { get; set; }

    /// <summary>
    /// Gets or sets the frames.
    /// </summary>
    /// <value>
    /// The frames.
    /// </value>
    public IReadOnlyList<TiffFrameMetadata> Frames { get; set; } = Array.Empty<TiffFrameMetadata>();

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new TiffMetadata(this);
}
