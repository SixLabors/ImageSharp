// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the wedge search measures against the scalar definitions of libaom across intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1WedgeSearchTests
{
    /// <summary>
    /// The hardware configurations that run every register width and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// The sample counts of the wedge block sizes, from 8x8 to 32x32, and a count with a scalar tail.
    /// </summary>
    private static readonly int[] Counts = [64, 128, 256, 512, 1024, 72];

    /// <summary>
    /// Verifies av1_wedge_compute_delta_squares(), av1_wedge_sign_from_residuals() and
    /// av1_wedge_sse_from_residuals().
    /// </summary>
    [Fact]
    public void WedgeMeasuresMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateWedgeMeasures, Configurations);

    private static void ValidateWedgeMeasures()
    {
        Random random = new(0x3ED6);
        foreach (int limit in new[] { 255, 1023, 4095, short.MaxValue })
        {
            foreach (int count in Counts)
            {
                short[] first = CreateResiduals(random, count, limit);
                short[] second = CreateResiduals(random, count, limit);
                short[] difference = CreateResiduals(random, count, limit);
                byte[] mask = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    mask[i] = (byte)random.Next(0, 65);
                }

                short[] expectedDelta = new short[count];
                for (int i = 0; i < count; i++)
                {
                    expectedDelta[i] = (short)Math.Clamp((first[i] * first[i]) - (second[i] * second[i]), short.MinValue, short.MaxValue);
                }

                // The destination can be the first residual, as in pick_wedge().
                short[] actualDelta = (short[])first.Clone();
                Av1WedgeSearch.ComputeDeltaSquares(actualDelta, actualDelta, second);
                Assert.Equal(expectedDelta, actualDelta);

                long weighted = 0;
                for (int i = 0; i < count; i++)
                {
                    weighted += mask[i] * expectedDelta[i];
                }

                Assert.True(Av1WedgeSearch.GetSign(expectedDelta, mask, weighted - 1));
                Assert.False(Av1WedgeSearch.GetSign(expectedDelta, mask, weighted));

                ulong squares = 0;
                for (int i = 0; i < count; i++)
                {
                    int value = Math.Clamp((64 * second[i]) + (mask[i] * difference[i]), short.MinValue, short.MaxValue);
                    squares += (ulong)((long)value * value);
                }

                Assert.Equal((squares + 2048) >> 12, Av1WedgeSearch.SumSquaredErrors(second, difference, mask));
            }
        }
    }

    private static short[] CreateResiduals(Random random, int count, int limit)
    {
        short[] values = new short[count];
        for (int i = 0; i < count; i++)
        {
            // Every eighth value is an extreme, to reach the saturation of each measure.
            values[i] = i % 8 == 0 ? (short)(i % 16 == 0 ? -limit - (limit == short.MaxValue ? 1 : 0) : limit) : (short)random.Next(-limit, limit + 1);
        }

        return values;
    }
}
