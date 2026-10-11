// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder;

public class GammaCorrectionTests
{
    [Fact]
    public void TestLinearToSRgbEdgeCases()
    {
        Assert.Equal(0, JxlGammaCorrect.LinearToSRgb8Direct(0.0));

        Assert.True(new TolerantMath(2E-5).AreEqual(0, JxlGammaCorrect.LinearToSRgb8Direct(1E-6)));

        Assert.Equal(0, JxlGammaCorrect.LinearToSRgb8Direct(-1E-6));
        Assert.Equal(0, JxlGammaCorrect.LinearToSRgb8Direct(-1E6));

        Assert.True(new TolerantMath(1E-5).AreEqual(1, JxlGammaCorrect.LinearToSRgb8Direct(1 - 1E-6)));

        Assert.Equal(1, JxlGammaCorrect.LinearToSRgb8Direct(1 + 1E-6));
        Assert.Equal(1, JxlGammaCorrect.LinearToSRgb8Direct(1E6));
    }

    [Fact]
    public void TestRoundTrip()
    {
        for (double linear = 0.0; linear <= 1.0; linear += 1E-7)
        {
            double srgb = JxlGammaCorrect.LinearToSRgb8Direct(linear);
            double linear2 = JxlGammaCorrect.SRgb8ToLinearDirect(srgb);

            Assert.True(Math.Abs(linear - linear2) < 2E-13, $"Linear = {linear}, Linear2 = {linear2}");
        }
    }
}
