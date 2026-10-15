// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jxl;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl;

public class JxlImageFormatDetectorTests
{
    [Fact]
    public void DetectCodestreamSignature()
    {
        var detector = new JxlImageFormatDetector();
        byte[] header = [0xFF, 0x0A, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(detector.TryDetectFormat(header.AsSpan(0, detector.HeaderSize), out IImageFormat format));
        Assert.Equal(JxlFormat.Instance, format);
    }

    [Fact]
    public void DetectContainerSignature()
    {
        var detector = new JxlImageFormatDetector();
        byte[] header = [0x00, 0x00, 0x00, 0x0C, (byte)'J', (byte)'X', (byte)'L', (byte)' ', 0x0D, 0x0A, 0x87, 0x0A];
        Assert.True(detector.TryDetectFormat(header.AsSpan(0, detector.HeaderSize), out IImageFormat format));
        Assert.Equal(JxlFormat.Instance, format);
    }

    [Fact]
    public void RejectJpegSignature()
    {
        var detector = new JxlImageFormatDetector();
        byte[] header = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.False(detector.TryDetectFormat(header.AsSpan(0, detector.HeaderSize), out _));
    }
}
