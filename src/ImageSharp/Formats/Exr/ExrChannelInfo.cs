// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Exr.Constants;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Information about a pixel channel.
/// </summary>
[DebuggerDisplay("Name: {ChannelName}, PixelType: {PixelType}")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct ExrChannelInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrChannelInfo" /> struct.
    /// </summary>
    /// <param name="channelName">Name of the channel.</param>
    /// <param name="pixelType">The type of the pixel data.</param>
    /// <param name="linear">Linear flag, possible values are 0 and 1.</param>
    /// <param name="xSampling">X sampling.</param>
    /// <param name="ySampling">Y sampling.</param>
    public ExrChannelInfo(string channelName, ExrPixelType pixelType, byte linear, int xSampling, int ySampling)
    {
        this.ChannelName = channelName;
        this.PixelType = pixelType;
        this.Linear = linear;
        this.XSampling = xSampling;
        this.YSampling = ySampling;
    }

    /// <summary>
    /// Gets the channel name.
    /// </summary>
    public string ChannelName { get; }

    /// <summary>
    /// Gets the type of the pixel data.
    /// </summary>
    public ExrPixelType PixelType { get; }

    /// <summary>
    /// Gets the linear flag. Hint to lossy compression methods that indicates whether
    /// human perception of the quantity represented by this channel
    /// is closer to linear or closer to logarithmic.
    /// </summary>
    public byte Linear { get; }

    /// <summary>
    /// Gets the x sampling value.
    /// </summary>
    public int XSampling { get; }

    /// <summary>
    /// Gets the y sampling value.
    /// </summary>
    public int YSampling { get; }
}
