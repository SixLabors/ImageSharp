// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder;

public class ChromaFromLumaEncoderTests
{
    /// <summary>
    /// Checks the native rounding rule where an estimated correlation reaches an exact midpoint after the zero bias.
    /// </summary>
    /// <param name="estimate">The unregularized multiplier estimate.</param>
    /// <param name="expected">The encoded integer adjustment.</param>
    [Theory]
    [InlineData(3.1F, 1)]
    [InlineData(-3.1F, -1)]
    [InlineData(5.1F, 3)]
    [InlineData(-5.1F, -3)]
    public void FindBestMultiplier_RoundsMidpointsAwayFromZero(float estimate, int expected)
    {
        // A luma coefficient equal to the default color factor makes the normalized quadratic coefficient one.
        // With no base or regularization, the estimate equals chroma. Subtracting the 2.6 bias leaves an exact half-integer.
        float[] luma = [JxlChromaFromLuma.DefaultColorFactor];
        float[] chroma = [estimate];

        int actual = JxlChromaFromLumaEncoder.FindBestMultiplier(luma, chroma, 1, 0F, 0F, true);

        Assert.Equal(expected, actual);
    }
}
