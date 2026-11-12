// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
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
    /// Checks that affine translation remains after cancellation in every SIMD width.
    /// </summary>
    [Fact]
    public void MatrixTransformRetainsTranslationAfterCancellation()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            VerifyMatrixTranslation,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Verifies an exact affine result that changes if translation is accumulated before the input terms.
    /// </summary>
    private static void VerifyMatrixTranslation()
    {
        Matrix4x4 matrix = Matrix4x4.Identity;
        matrix.M11 = 1F;
        matrix.M21 = 1F;
        matrix.M31 = 0F;
        matrix.M41 = 1F / 1024F;

        // The two large binary-exact products cancel. The final translation must survive unchanged;
        // adding it to 32768 first would round it away. No numerical tolerance is needed for these values.
        SimdUtils.Transform3(in matrix, Vector128.Create(32768F), Vector128.Create(-32768F), Vector128<float>.Zero, out Vector128<float> x128, out _, out _);
        Assert.Equal(Vector128.Create(matrix.M41), x128);

        SimdUtils.Transform3(in matrix, Vector256.Create(32768F), Vector256.Create(-32768F), Vector256<float>.Zero, out Vector256<float> x256, out _, out _);
        Assert.Equal(Vector256.Create(matrix.M41), x256);

        SimdUtils.Transform3(in matrix, Vector512.Create(32768F), Vector512.Create(-32768F), Vector512<float>.Zero, out Vector512<float> x512, out _, out _);
        Assert.Equal(Vector512.Create(matrix.M41), x512);
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
