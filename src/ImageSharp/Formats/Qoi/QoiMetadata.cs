// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Provides Qoi specific metadata information for the image.
/// </summary>
public class QoiMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QoiMetadata"/> class.
    /// </summary>
    public QoiMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="QoiMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    public QoiMetadata(QoiMetadata other)
    {
        this.Width = other.Width;
        this.Height = other.Height;
        this.Channels = other.Channels;
        this.ColorSpace = other.ColorSpace;
    }

    /// <summary>
    /// Gets or sets image width in pixels (BE)
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets image height in pixels (BE)
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets color channels of the image. 3 = RGB, 4 = RGBA.
    /// </summary>
    public QoiChannels Channels { get; set; }

    /// <summary>
    /// Gets or sets color space of the image. 0 = sRGB with linear alpha, 1 = All channels linear
    /// </summary>
    public QoiColorSpace ColorSpace { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new QoiMetadata(this);
}
