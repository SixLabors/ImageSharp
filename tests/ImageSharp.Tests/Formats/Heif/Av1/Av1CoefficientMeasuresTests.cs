// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the coefficient magnitude measures against scalar definitions across intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CoefficientMeasuresTests
{
    /// <summary>
    /// The hardware configurations that run every register width and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the sum of magnitudes (aom_satd), the largest magnitude, the end of block and the level bits.
    /// </summary>
    [Fact]
    public void MeasuresMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateMeasures, Configurations);

    private static void ValidateMeasures()
    {
        Random random = new(0xC0EF);
        foreach (int length in new[] { 0, 1, 3, 4, 15, 16, 17, 63, 64, 1023, 1024, 4096 })
        {
            foreach (int limit in new[] { 255, 1 << 18 })
            {
                int[] coefficients = new int[length];
                for (int i = 0; i < length; i++)
                {
                    coefficients[i] = random.Next(-limit, limit + 1);
                }

                // The largest magnitude sits at a random position, so every lane and the scalar tail can hold it.
                if (length > 0)
                {
                    coefficients[random.Next(length)] = -(limit + 1);
                }

                long expectedSum = 0;
                int expectedMaximum = 0;
                foreach (int coefficient in coefficients)
                {
                    expectedSum += Math.Abs(coefficient);
                    expectedMaximum = Math.Max(expectedMaximum, Math.Abs(coefficient));
                }

                Assert.Equal(expectedSum, Av1CoefficientMeasures.SumAbsolute(coefficients));
                Assert.Equal(expectedMaximum, Av1CoefficientMeasures.GetMaximumAbsolute(coefficients));

                // A random scan order, with most coefficients zero, so the last nonzero position varies.
                short[] inverseScan = new short[length];
                for (int i = 0; i < length; i++)
                {
                    inverseScan[i] = (short)i;
                }

                random.Shuffle(inverseScan);
                for (int i = 0; i < length; i++)
                {
                    if (random.Next(8) != 0)
                    {
                        coefficients[i] = 0;
                    }
                }

                int expectedEnd = 0;
                for (int i = 0; i < length; i++)
                {
                    if (coefficients[i] != 0)
                    {
                        expectedEnd = Math.Max(expectedEnd, inverseScan[i] + 1);
                    }
                }

                Assert.Equal(expectedEnd, Av1CoefficientMeasures.GetEndOfBlock(coefficients, inverseScan));

                // The level bits of rate_estimator(), with one magnitude above 2^30 so every logarithm step is taken.
                if (length > 0)
                {
                    coefficients[random.Next(length)] = (1 << 30) + random.Next(1 << 20);
                }

                int expectedBits = 0;
                foreach (int coefficient in coefficients)
                {
                    int level = Math.Abs(coefficient);
                    expectedBits += (int)Math.Floor(Math.Log2(level + 1.0)) + (level > 0 ? 1 : 0);
                }

                Assert.Equal(expectedBits, Av1CoefficientMeasures.SumLevelBits(coefficients));

                // The reconstruction wraps to int16 in low precision, as av1_block_error_lp stores it.
                int[] reconstructed = new int[length];
                for (int i = 0; i < length; i++)
                {
                    reconstructed[i] = coefficients[i] + random.Next(-70000, 70001);
                }

                long expectedWide = 0;
                long expectedNarrow = 0;
                for (int i = 0; i < length; i++)
                {
                    long wide = (long)coefficients[i] - reconstructed[i];
                    long narrow = (long)coefficients[i] - (short)reconstructed[i];
                    expectedWide += wide * wide;
                    expectedNarrow += narrow * narrow;
                }

                Assert.Equal(expectedWide, Av1CoefficientMeasures.SumSquaredDifferences(coefficients, reconstructed, false));
                Assert.Equal(expectedNarrow, Av1CoefficientMeasures.SumSquaredDifferences(coefficients, reconstructed, true));
            }
        }
    }
}
