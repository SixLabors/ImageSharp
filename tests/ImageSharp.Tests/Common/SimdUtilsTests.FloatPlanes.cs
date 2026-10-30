// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
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
    /// Checks signed conversion, independent component normalization and opaque output without an alpha plane.
    /// </summary>
    /// <param name="count">The number of samples, including a scalar tail.</param>
    [Theory]
    [InlineData(3)]
    [InlineData(31)]
    public void PackedNormalizeInterleave4_Int32AcrossSimdAndTail(int count)
    {
        static void RunTest(string serialized) => AssertPackedNormalizeInterleaveInt32(FeatureTestRunner.Deserialize<int>(serialized));

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            count,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks each packed component against signed integer conversion followed by division by its own maximum.
    /// </summary>
    /// <param name="count">The number of samples to convert.</param>
    private static void AssertPackedNormalizeInterleaveInt32(int count)
    {
        int[] samples = [int.MinValue, int.MaxValue, -16777217, 16777217, -3, -1, 0, 1, 7];
        int[][] components = [new int[count], new int[count], new int[count], new int[count]];
        for (int i = 0; i < count; i++)
        {
            for (int component = 0; component < components.Length; component++)
            {
                components[component][i] = samples[(i + component) % samples.Length];
            }
        }

        // Different channel offsets expose lane-order mistakes. Values beyond 24 bits also check float conversion rounding.
        Vector4 maximum = new(2F, 4F, 8F, 16F);
        Vector4 sentinel = new(11F, 13F, 17F, 19F);
        Vector4[] destination = new Vector4[count + 1];
        destination[count] = sentinel;
        Span<float> packed = MemoryMarshal.Cast<Vector4, float>(destination.AsSpan());
        SimdUtils.PackedNormalizeInterleave4(components[0], components[1], components[2], components[3], packed, maximum);

        for (int i = 0; i < count; i++)
        {
            Assert.Equal((float)components[0][i] / maximum.X, destination[i].X);
            Assert.Equal((float)components[1][i] / maximum.Y, destination[i].Y);
            Assert.Equal((float)components[2][i] / maximum.Z, destination[i].Z);
            Assert.Equal((float)components[3][i] / maximum.W, destination[i].W);
        }

        Assert.Equal(sentinel, destination[count]);

        // Missing alpha supplies one before storage. Its unused maximum must not alter that value, even when it is NaN.
        maximum.W = float.NaN;
        SimdUtils.PackedNormalizeInterleave4(components[0], components[1], components[2], default, packed, maximum);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal((float)components[0][i] / maximum.X, destination[i].X);
            Assert.Equal((float)components[1][i] / maximum.Y, destination[i].Y);
            Assert.Equal((float)components[2][i] / maximum.Z, destination[i].Z);
            Assert.Equal(1F, destination[i].W);
        }

        Assert.Equal(sentinel, destination[count]);
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

        // Check the packed layout before reversing it; paired transpose mistakes could otherwise cancel.
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(components[0][i]), BitConverter.SingleToInt32Bits(vectors[i].X));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[1][i]), BitConverter.SingleToInt32Bits(vectors[i].Y));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[2][i]), BitConverter.SingleToInt32Bits(vectors[i].Z));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[3][i]), BitConverter.SingleToInt32Bits(vectors[i].W));
        }

        float[][] output = [new float[count], new float[count], new float[count], new float[count]];
        SimdUtils.DeinterleaveFloatPlanes(vectors, output[0], output[1], output[2], output[3]);

        for (int component = 0; component < 4; component++)
        {
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(components[component][i]), BitConverter.SingleToInt32Bits(output[component][i]));
            }
        }

        // A native extra channel occupies X and uses W for its selected weight. Missing Y/Z planes must supply zero
        // without allocating dummy rows. Check packing before unpacking so inverse mistakes cannot cancel.
        SimdUtils.InterleaveFloatPlanes(components[0], default, default, components[3], vectors);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(components[0][i]), BitConverter.SingleToInt32Bits(vectors[i].X));
            Assert.Equal(0, BitConverter.SingleToInt32Bits(vectors[i].Y));
            Assert.Equal(0, BitConverter.SingleToInt32Bits(vectors[i].Z));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[3][i]), BitConverter.SingleToInt32Bits(vectors[i].W));
        }

        SimdUtils.DeinterleaveFloatPlanes(vectors, output[0], default, default, output[3]);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(BitConverter.SingleToInt32Bits(components[0][i]), BitConverter.SingleToInt32Bits(output[0][i]));
            Assert.Equal(BitConverter.SingleToInt32Bits(components[3][i]), BitConverter.SingleToInt32Bits(output[3][i]));
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
