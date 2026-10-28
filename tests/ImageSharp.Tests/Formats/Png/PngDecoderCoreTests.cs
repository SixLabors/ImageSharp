// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngDecoderCoreTests
{
    [Fact]
    public void CalculateScanlineLength_WithLargeGrayscaleWidth_ReturnsExpectedLength()
    {
        int length = PngDecoderCore.CalculateScanlineLength(536_870_913, 8, 1);

        Assert.Equal(536_870_913, length);
    }

    [Fact]
    public void CalculateScanlineLength_WithLargeRgbaWidth_ReturnsExpectedLength()
    {
        int length = PngDecoderCore.CalculateScanlineLength(33_554_432, 16, 8);

        Assert.Equal(268_435_456, length);
    }
}
