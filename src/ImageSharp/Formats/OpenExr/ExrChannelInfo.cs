// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.OpenExr;

[DebuggerDisplay("Name: {ChannelName}, PixelType: {PixelType}")]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct ExrChannelInfo
{
    public ExrChannelInfo(string channelName, ExrPixelType pixelType, byte pLinear, int xSampling, int ySampling)
    {
        this.ChannelName = channelName;
        this.PixelType = pixelType;
        this.PLinear = pLinear;
        this.XSampling = xSampling;
        this.YSampling = ySampling;
    }

    public string ChannelName { get; }

    public ExrPixelType PixelType { get; }

    public byte PLinear { get; }

    public int XSampling { get; }

    public int YSampling { get; }
}
