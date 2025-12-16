// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests.Formats.Tiff.Utils;

[Trait("Format", "Tiff")]
public class TiffUtilitiesTest
{
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(42, 84, 128, 0)]
    [InlineData(65535, 65535, 65535, 0)]
    public void ColorFromRgba64Premultiplied_WithZeroAlpha_ReturnsDefaultPixel(ushort r, ushort g, ushort b, ushort a)
    {
        Rgba64 actual = TiffUtilities.ColorFromRgba64Premultiplied<Rgba64>(r, g, b, a);

        Assert.Equal(default, actual);
    }

    [Theory]
    [InlineData(65535, 65535, 65535, 65535, 65535, 65535, 65535, 65535)]
    [InlineData(32767, 0, 0, 65535, 32767, 0, 0, 65535)]
    [InlineData(0, 32767, 0, 65535, 0, 32767, 0, 65535)]
    [InlineData(0, 0, 32767, 65535, 0, 0, 32767, 65535)]
    public void ColorFromRgba64Premultiplied_WithNoAlpha_ReturnExpectedValues(ushort r, ushort g, ushort b, ushort a, ushort expectedR, ushort expectedG, ushort expectedB, ushort expectedA)
    {
        Rgba64 actual = TiffUtilities.ColorFromRgba64Premultiplied<Rgba64>(r, g, b, a);

        Assert.Equal(new Rgba64(expectedR, expectedG, expectedB, expectedA), actual);
    }

    [Theory]
    [InlineData(32766, 0, 0, 32766, 65535, 0, 0, 32766)] // Red, 50% Alpha
    [InlineData(0, 32766, 0, 32766, 0, 65535, 0, 32766)] // Green, 50% Alpha
    [InlineData(0, 0, 32766, 32766, 0, 0, 65535, 32766)] // Blue, 50% Alpha
    [InlineData(8191, 0, 0, 16383, 32765, 0, 0, 16383)] // Red, 25% Alpha
    [InlineData(0, 8191, 0, 16383, 0, 32765, 0, 16383)] // Green, 25% Alpha
    [InlineData(0, 0, 8191, 16383, 0, 0, 32765, 16383)] // Blue, 25% Alpha
    [InlineData(8191, 0, 0, 0, 0, 0, 0, 0)] // Red, 0% Alpha
    [InlineData(0, 8191, 0, 0, 0, 0, 0, 0)] // Green, 0% Alpha
    [InlineData(0, 0, 8191, 0, 0, 0, 0, 0)] // Blue, 0% Alpha
    public void ColorFromRgba64Premultiplied_WithAlpha_ReturnExpectedValues(ushort r, ushort g, ushort b, ushort a, ushort expectedR, ushort expectedG, ushort expectedB, ushort expectedA)
    {
        Rgba64 actual = TiffUtilities.ColorFromRgba64Premultiplied<Rgba64>(r, g, b, a);

        Assert.Equal(new Rgba64(expectedR, expectedG, expectedB, expectedA), actual);
    }
}
