// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1MathTests
{
    /// <summary>
    /// Verifies the logarithm, bit, and clip helpers against their definitions at powers of two and their neighbors.
    /// </summary>
    [Fact]
    public void HelpersMatchDefinition()
    {
        uint[] values = [1, 2, 3, 4, 5, 7, 8, 9];
        uint[] floorLog2 = [0, 1, 1, 2, 2, 2, 3, 3];
        uint[] ceilLog2 = [0, 1, 2, 2, 3, 3, 3, 4];
        for (int i = 0; i < values.Length; i++)
        {
            Assert.Equal((int)floorLog2[i], Av1Math.MostSignificantBit(values[i]));
            Assert.Equal((int)floorLog2[i], Av1Math.Log2((int)values[i]));
            Assert.Equal(floorLog2[i], Av1Math.FloorLog2(values[i]));
            Assert.Equal(floorLog2[i], Av1Math.Log2_32(values[i]));
            Assert.Equal(ceilLog2[i], Av1Math.CeilLog2(values[i]));
        }

        Assert.Equal(1, Av1Math.GetBit(4, 2));
        Assert.Equal(0, Av1Math.GetBit(4, 3));
        Assert.Equal(1, Av1Math.GetBit(9, 0));
        Assert.Equal(0, Av1Math.GetBit(8, 0));

        int value = 4;
        Av1Math.SetBit(ref value, 2);
        Assert.Equal(4, value);
        Av1Math.SetBit(ref value, 3);
        Assert.Equal(12, value);

        Assert.Equal(4, Av1Math.Clip3(0, 255, 4));
        Assert.Equal(0, Av1Math.Clip3(0, 255, -1));
        Assert.Equal(255, Av1Math.Clip3(0, 255, 255));
        Assert.Equal(255, Av1Math.Clip3(0, 255, 256));
    }
}
