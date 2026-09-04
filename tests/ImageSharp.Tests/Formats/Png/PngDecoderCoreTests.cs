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
}
