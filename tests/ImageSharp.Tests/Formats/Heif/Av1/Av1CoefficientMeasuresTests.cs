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
    /// Verifies the sum of magnitudes (aom_satd) and the largest magnitude.
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
            }
        }
    }
}
