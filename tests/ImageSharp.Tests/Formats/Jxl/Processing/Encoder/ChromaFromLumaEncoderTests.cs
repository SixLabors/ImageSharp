// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.ChromaFromLuma;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing.Encoder;

public class ChromaFromLumaEncoderTests
{
    /// <summary>
    /// Checks logical counts, signed derivatives and scalar tails at every supported vector width.
    /// </summary>
    [Fact]
    public void CorrelationLoss_UsesEveryLogicalObservation()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> lengths = [0, 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 65];
            ReadOnlySpan<int> signs = [-1, 1];
            foreach (int length in lengths)
            {
                float[] luma = new float[length + 1];
                float[] chroma = new float[length + 1];
                luma.AsSpan(0, length).Fill(JxlChromaFromLuma.DefaultColorFactor);

                // The extra pair is storage padding, not an observation. Reading it would introduce NaN into the loss.
                luma[length] = float.MaxValue;
                chroma[length] = float.NaN;

                foreach (int sign in signs)
                {
                    // Normalized luma is one. A residual of +/-0.5 has derivative +/-1 under the one-third loss.
                    // With eps = 0, all three sums must equal the signed number of observations, without rounding tolerance.
                    JxlCflFunction function = new(luma, chroma, length, 0F, 0F);
                    float plus = float.NaN;
                    float minus = float.NaN;
                    float actual = function.Compute(sign * 0.5F, 0F, ref plus, ref minus);
                    Assert.Equal((float)(sign * length), actual);
                    Assert.Equal((float)(sign * length), plus);
                    Assert.Equal((float)(sign * length), minus);
                }
            }

            // At the cutoff, the native loss excludes all three terms according to the central residual.
            // The displaced residuals straddle the cutoff, so applying three independent cutoff tests would fail here.
            float[] cutoffLuma = new float[33];
            float[] cutoffChroma = new float[33];
            cutoffLuma.AsSpan().Fill(JxlChromaFromLuma.DefaultColorFactor);
            cutoffChroma.AsSpan().Fill(-99.5F);
            JxlCflFunction cutoffFunction = new(cutoffLuma, cutoffChroma, cutoffLuma.Length, 0F, 0F);
            float cutoffPlus = float.NaN;
            float cutoffMinus = float.NaN;
            Assert.Equal(0F, cutoffFunction.Compute(0.5F, 1F, ref cutoffPlus, ref cutoffMinus));
            Assert.Equal(0F, cutoffPlus);
            Assert.Equal(0F, cutoffMinus);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

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
