// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the flow field upscale passes.
/// </summary>
[Trait("Format", "Avif")]
public class Av1FlowFieldTests
{
    /// <summary>
    /// The hardware configurations covering every vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics Configurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    private static readonly double[] LowerPhase = [-3 / 128.0, 29 / 128.0, 111 / 128.0, -9 / 128.0];

    private static readonly double[] UpperPhase = [-9 / 128.0, 111 / 128.0, 29 / 128.0, -3 / 128.0];

    /// <summary>
    /// Verifies both passes bit for bit against the sequential tap sums of upscale_flow_component(), for widths that
    /// end in every vector tail.
    /// </summary>
    [Fact]
    public void UpscalePassesMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(AssertUpscalePasses, Configurations);

    private static void AssertUpscalePasses()
    {
        const int border = Av1FlowField.BorderOuter;
        int seed = 11;
        foreach (int width in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 13, 16, 31, 60 })
        {
            double[] input = new double[width + (2 * border)];
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = Next(ref seed);
            }

            double[] output = new double[2 * width];
            Av1FlowField.UpscaleRow(input, output);
            for (int column = 0; column < width; column++)
            {
                double left = 0;
                double right = 0;
                for (int tap = 0; tap < 4; tap++)
                {
                    left += input[column + tap] * LowerPhase[tap];
                    right += input[column + tap + 1] * UpperPhase[tap];
                }

                Assert.Equal(BitConverter.DoubleToInt64Bits(2.0 * left), BitConverter.DoubleToInt64Bits(output[2 * column]));
                Assert.Equal(BitConverter.DoubleToInt64Bits(2.0 * right), BitConverter.DoubleToInt64Bits(output[(2 * column) + 1]));
            }

            int upscaledWidth = 2 * width;
            int stride = upscaledWidth + 3;
            double[] rows = new double[(5 * stride) + upscaledWidth];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = Next(ref seed);
            }

            double[] top = new double[upscaledWidth];
            double[] bottom = new double[upscaledWidth];
            Av1FlowField.UpscaleColumns(rows, stride, top, bottom);
            for (int column = 0; column < upscaledWidth; column++)
            {
                double upper = 0;
                double lower = 0;
                for (int tap = 0; tap < 4; tap++)
                {
                    upper += rows[(tap * stride) + column] * LowerPhase[tap];
                    lower += rows[((tap + 1) * stride) + column] * UpperPhase[tap];
                }

                Assert.Equal(BitConverter.DoubleToInt64Bits(upper), BitConverter.DoubleToInt64Bits(top[column]));
                Assert.Equal(BitConverter.DoubleToInt64Bits(lower), BitConverter.DoubleToInt64Bits(bottom[column]));
            }
        }
    }

    private static double Next(ref int seed)
    {
        seed = (seed * 1103515245) + 12345;
        return (((seed >>> 8) & 0xFFFFF) - 0x80000) / 3171.0;
    }
}
