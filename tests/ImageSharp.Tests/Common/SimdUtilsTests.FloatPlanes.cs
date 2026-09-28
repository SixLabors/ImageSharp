// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

public partial class SimdUtilsTests
{
    [Theory]
    [MemberData(nameof(ArbitraryArraySizes))]
    public void FloatPlanes_PreserveComponentBitsAcrossSimdAndTail(int count)
    {
        static void RunTest(string serialized) => AssertFloatPlanes(FeatureTestRunner.Deserialize<int>(serialized));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks all four component planes and an omitted fourth plane against exact sample bits.
    /// </summary>
    /// <param name="count">The number of pixels to transpose.</param>
    private static void AssertFloatPlanes(int count)
    {
        float[] samples =
        [
            BitConverter.Int32BitsToSingle(unchecked((int)0x7FC01234)),
            float.PositiveInfinity,
            float.NegativeInfinity,
            BitConverter.Int32BitsToSingle(unchecked((int)0x80000000)),
            -2.5F,
            0.1F,
            1F,
            65504F
        ];

        float[][] components = [new float[count], new float[count], new float[count], new float[count]];
        for (int i = 0; i < count; i++)
        {
            for (int component = 0; component < 4; component++)
            {
                components[component][i] = samples[(i + component) % samples.Length];
            }
        }

        Vector4[] vectors = new Vector4[count];
        SimdUtils.InterleaveFloatPlanes(components[0], components[1], components[2], components[3], vectors);

        float[][] output = [new float[count], new float[count], new float[count], new float[count]];
        SimdUtils.DeinterleaveFloatPlanes(vectors, output[0], output[1], output[2], output[3]);

        for (int component = 0; component < 4; component++)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(components[component][i]), BitConverter.SingleToInt32Bits(output[component][i]));
            }
        }

        // An absent fourth plane must supply opaque values in both full registers and tails.
        SimdUtils.InterleaveFloatPlanes(components[0], components[1], components[2], ReadOnlySpan<float>.Empty, vectors);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(components[0][i]), BitConverter.SingleToInt32Bits(vectors[i].X));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[1][i]), BitConverter.SingleToInt32Bits(vectors[i].Y));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[2][i]), BitConverter.SingleToInt32Bits(vectors[i].Z));
            Assert.Equal(1F, vectors[i].W);
        }
    }
}
