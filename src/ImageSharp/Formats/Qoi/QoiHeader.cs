// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Text;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Represents the qoi header chunk.
/// </summary>
internal readonly struct QoiHeader
{
    public QoiHeader(uint width, uint height, QoiChannels channels, QoiColorSpace colorSpace)
    {
        this.Width = width;
        this.Height = height;
        this.Channels = channels;
        this.ColorSpace = colorSpace;
    }

    /// <summary>
    /// Gets the magic bytes "qoif"
    /// </summary>
    public byte[] Magic { get; } = Encoding.UTF8.GetBytes("qoif");

    /// <summary>
    /// Gets the image width in pixels (Big Endian)
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the image height in pixels (Big Endian)
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the color channels of the image. 3 = RGB, 4 = RGBA.
    /// </summary>
    public QoiChannels Channels { get; }

    /// <summary>
    /// Gets the color space of the image. 0 = sRGB with linear alpha, 1 = All channels linear
    /// </summary>
    public QoiColorSpace ColorSpace { get; }
}
