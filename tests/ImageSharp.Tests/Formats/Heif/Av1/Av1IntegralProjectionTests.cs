// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the integral projections against aom_int_pro_row_c, aom_int_pro_col_c and aom_vector_var_c across
/// intrinsic tiers.
/// </summary>
[Trait("Format", "Avif")]
public class Av1IntegralProjectionTests
{
    /// <summary>
    /// The hardware configurations that run every register width and the scalar overloads.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies the column sums, the row sums and the projection variance.
    /// </summary>
    [Fact]
    public void ProjectionsMatchReference()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateProjections, Configurations);

    private static void ValidateProjections()
    {
        Random random = new(0x1970);
        foreach (int width in new[] { 16, 32, 64, 128, 160, 24 })
        {
            foreach (int height in new[] { 16, 32, 64, 128 })
            {
                int stride = width + 11;
                byte[] samples = new byte[stride * height];
                random.NextBytes(samples);

                // Extreme columns and rows reach the sixteen-bit bound of the sums.
                for (int y = 0; y < height; y++)
                {
                    samples[y * stride] = 255;
                }

                int columnShift = BitOperations.Log2((uint)height) - 1;
                int rowShift = 3 + (Math.Min(width, 128) >> 5);
                short[] expectedColumns = new short[width];
                for (int x = 0; x < width; x++)
                {
                    int sum = 0;
                    for (int y = 0; y < height; y++)
                    {
                        sum += samples[(y * stride) + x];
                    }

                    expectedColumns[x] = (short)(sum >> columnShift);
                }

                short[] columns = new short[width];
                Av1IntegralProjection.ProjectColumns(columns, samples, stride, width, height, columnShift);
                Assert.Equal(expectedColumns, columns);

                int rowWidth = Math.Min(width, 128);
                short[] expectedRows = new short[height];
                for (int y = 0; y < height; y++)
                {
                    int sum = 0;
                    for (int x = 0; x < rowWidth; x++)
                    {
                        sum += samples[(y * stride) + x];
                    }

                    expectedRows[y] = (short)(sum >> rowShift);
                }

                short[] rows = new short[height];
                Av1IntegralProjection.ProjectRows(rows, samples, stride, rowWidth, height, rowShift);
                Assert.Equal(expectedRows, rows);
            }
        }

        foreach (int length in new[] { 16, 32, 64, 128 })
        {
            short[] reference = new short[length + 5];
            short[] source = new short[length];
            for (int i = 0; i < reference.Length; i++)
            {
                reference[i] = (short)random.Next(0, 511);
            }

            for (int i = 0; i < length; i++)
            {
                source[i] = (short)random.Next(0, 511);
            }

            // The largest mean: every difference is 510.
            short[] extreme = new short[length];
            Array.Fill(extreme, (short)510);
            Assert.Equal(VectorVarianceReference(extreme, new short[length]), Av1IntegralProjection.GetVariance(extreme, new short[length]));
            Assert.Equal(VectorVarianceReference(reference, source), Av1IntegralProjection.GetVariance(reference, source));
        }
    }

    /// <summary>
    /// The scalar definition of aom_vector_var_c.
    /// </summary>
    private static int VectorVarianceReference(short[] reference, short[] source)
    {
        int sse = 0;
        int mean = 0;
        for (int i = 0; i < source.Length; i++)
        {
            int difference = reference[i] - source[i];
            mean += difference;
            sse += difference * difference;
        }

        uint magnitude = (uint)Math.Abs(mean);
        return (int)((uint)sse - ((magnitude * magnitude) >> BitOperations.Log2((uint)source.Length)));
    }
}
