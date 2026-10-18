// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Common;

/// <summary>
/// Tests shared planar and packed color-buffer transformations.
/// </summary>
public partial class SimdUtilsTests
{
    private static readonly int[] Lengths = [0, 1, 2, 3, 4, 5, 7, 8, 15, 16, 17, 31, 32, 33, 129];

    /// <summary>
    /// Checks saturated sixteen-bit packing and midpoint-to-even rounding across all SIMD widths.
    /// </summary>
    [Fact]
    public void UInt16PackingMatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateUInt16Packing,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks complete vector groups, scalar tails, code midpoints, nonfinite values and destination padding.
    /// </summary>
    private static void ValidateUInt16Packing()
    {
        foreach (int length in Lengths.Append(ushort.MaxValue + 1))
        {
            float[] source = new float[length];
            ushort[] expected = new ushort[length + 1];
            ushort[] actual = new ushort[length + 1];
            expected[^1] = actual[^1] = ushort.MaxValue;

            for (int i = 0; i < length; i++)
            {
                // Half-code inputs expose the rounding rule. The full-range case visits every sixteen-bit midpoint.
                source[i] = (i + .5F) / ushort.MaxValue;
            }

            if (length >= 4)
            {
                // Saturation belongs to final integer packing, including values outside the stored sample range.
                source[0] = -1F;
                source[1] = 2F;
                source[2] = float.NegativeInfinity;
                source[3] = float.PositiveInfinity;
            }

            if (length > 0)
            {
                // The final lane covers both complete SIMD groups and scalar tails. Safe packing maps NaN to zero.
                source[^1] = float.NaN;
            }

            for (int i = 0; i < length; i++)
            {
                expected[i] = float.IsNaN(source[i]) ? (ushort)0 : (ushort)MathF.Round(Numerics.ClampNumber(source[i], 0F, 1F) * ushort.MaxValue);
            }

            SimdUtils.NormalizedFloatToUInt16Saturate(source, actual);

            Assert.Equal(expected, actual);
        }
    }

    /// <summary>
    /// Checks bounded byte deinterleaving and signed and unsigned widening across hardware modes.
    /// </summary>
    [Fact]
    public void ByteTriplesPreserveComponents() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateByteTriples,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks exact-end loads, an unaligned triple offset, and both sides of the signed-byte boundary.
    /// </summary>
    private static void ValidateByteTriples()
    {
        // Seventeen codes prevent a misplaced four-, eight-, or sixteen-pixel group from matching by repetition.
        ReadOnlySpan<byte> samples = [0, 1, 2, 3, 7, 15, 31, 63, 127, 128, 129, 191, 223, 247, 253, 254, 255];

        foreach (int count in new[] { 8, 16 })
        {
            // Skip one complete triple. The last requested triple ends at the array boundary, with no readable padding.
            const int Start = 1;
            byte[] packed = new byte[(count + Start) * 3];
            for (int i = 0; i < packed.Length; i++)
            {
                packed[i] = samples[i % samples.Length];
            }

            byte[][] actual = [new byte[count], new byte[count], new byte[count]];
            int[] unsigned = new int[count];
            int[] signed = new int[count];

            if (count == 8)
            {
                SimdUtils.Deinterleave3(packed, Start, out Vector64<byte> x, out Vector64<byte> y, out Vector64<byte> z);
                x.StoreUnsafe(ref actual[0][0]);
                y.StoreUnsafe(ref actual[1][0]);
                z.StoreUnsafe(ref actual[2][0]);
                Vector256_.Widen(x).StoreUnsafe(ref unsigned[0]);
                Vector256_.Widen(x.AsSByte()).StoreUnsafe(ref signed[0]);
            }
            else
            {
                SimdUtils.Deinterleave3(packed, Start, out Vector128<byte> x, out Vector128<byte> y, out Vector128<byte> z);
                x.StoreUnsafe(ref actual[0][0]);
                y.StoreUnsafe(ref actual[1][0]);
                z.StoreUnsafe(ref actual[2][0]);

                // Native 512-bit helpers require the supported path; narrower modes exercise the portable 256-bit widening above.
                if (Vector512.IsHardwareAccelerated)
                {
                    Vector512_.Widen(x).StoreUnsafe(ref unsigned[0]);
                    Vector512_.Widen(x.AsSByte()).StoreUnsafe(ref signed[0]);
                }
            }

            for (int i = 0; i < count; i++)
            {
                for (int channel = 0; channel < actual.Length; channel++)
                {
                    Assert.Equal(packed[((i + Start) * 3) + channel], actual[channel][i]);
                }

                if (count == 8 || Vector512.IsHardwareAccelerated)
                {
                    // Signed widening interprets the byte as two's complement; it must not zero-extend negative chroma.
                    Assert.Equal((int)actual[0][i], unsigned[i]);
                    Assert.Equal((int)unchecked((sbyte)actual[0][i]), signed[i]);
                }
            }
        }
    }

    /// <summary>
    /// Checks exact word-triple packing and bounded loading with every SIMD width and its fallback.
    /// </summary>
    [Fact]
    public void WordTriplesPreserveComponents() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateWordTriples,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks component order and untouched destination sentinels with unaligned, exact-end source groups.
    /// </summary>
    private static void ValidateWordTriples()
    {
        Random random = new(42);

        foreach (int count in new[] { 8, 16, 32 })
        {
            // Offset each source by one word. The requested group ends at the array boundary, with no readable padding.
            ushort[][] planes = [new ushort[count + 1], new ushort[count + 1], new ushort[count + 1], new ushort[count + 1]];

            foreach (ushort[] plane in planes)
            {
                random.NextBytes(MemoryMarshal.AsBytes(plane.AsSpan()));
            }

            // Three leading sentinel words also make the destination unaligned. A trailing sentinel detects an oversized store.
            const int Start = 3;
            ushort[] expected = new ushort[(count * 3) + Start + 1];
            Array.Fill(expected, ushort.MaxValue);
            ushort[] actual = expected.ToArray();

            for (int i = 0; i < count; i++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    expected[Start + (i * 3) + channel] = planes[channel][i + 1];
                }
            }

            if (count == 8)
            {
                SimdUtils.Interleave3(
                    Vector128.LoadUnsafe(ref planes[0][1]),
                    Vector128.LoadUnsafe(ref planes[1][1]),
                    Vector128.LoadUnsafe(ref planes[2][1]),
                    ref actual[Start]);
            }
            else if (count == 16)
            {
                SimdUtils.Interleave3(
                    Vector256.LoadUnsafe(ref planes[0][1]),
                    Vector256.LoadUnsafe(ref planes[1][1]),
                    Vector256.LoadUnsafe(ref planes[2][1]),
                    ref actual[Start]);
            }
            else
            {
                SimdUtils.Interleave3(
                    Vector512.LoadUnsafe(ref planes[0][1]),
                    Vector512.LoadUnsafe(ref planes[1][1]),
                    Vector512.LoadUnsafe(ref planes[2][1]),
                    ref actual[Start]);
            }

            Assert.Equal(expected, actual);

            // Check loading against the independently interleaved source, not against the result of the packer under test.
            ushort[][] components = [new ushort[count], new ushort[count], new ushort[count]];

            if (count == 8)
            {
                SimdUtils.Deinterleave3(expected.AsSpan(Start), 0, out Vector128<ushort> x, out Vector128<ushort> y, out Vector128<ushort> z);
                x.StoreUnsafe(ref components[0][0]);
                y.StoreUnsafe(ref components[1][0]);
                z.StoreUnsafe(ref components[2][0]);
            }
            else if (count == 16)
            {
                SimdUtils.Deinterleave3(expected.AsSpan(Start), 0, out Vector256<ushort> x, out Vector256<ushort> y, out Vector256<ushort> z);
                x.StoreUnsafe(ref components[0][0]);
                y.StoreUnsafe(ref components[1][0]);
                z.StoreUnsafe(ref components[2][0]);
            }

            if (count != 32)
            {
                for (int channel = 0; channel < components.Length; channel++)
                {
                    Assert.Equal(planes[channel].AsSpan(1).ToArray(), components[channel]);
                }
            }
        }
    }

    /// <summary>
    /// Checks exact byte and word quadruple packing with hardware acceleration and portable fallbacks.
    /// </summary>
    [Fact]
    public void IntegerQuadruplesPreserveComponents() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        ValidateIntegerQuadruples,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks byte and word packing without allowing stores past the output.
    /// </summary>
    private static void ValidateIntegerQuadruples()
    {
        Random random = new(42);
        foreach (int count in new[] { 16, 32 })
        {
            ushort[][] planes = [new ushort[count + 1], new ushort[count + 1], new ushort[count + 1], new ushort[count + 1]];
            foreach (ushort[] plane in planes)
            {
                random.NextBytes(MemoryMarshal.AsBytes(plane.AsSpan()));
            }

            // Byte and ushort inputs exercise the same lane transpose at different component widths.
            byte[][] bytes = planes.Select(plane => MemoryMarshal.AsBytes(plane.AsSpan(1)).ToArray()).ToArray();
            byte[] expectedBytes = new byte[(count * 2 * 4) + 1];
            ushort[] expectedWords = new ushort[(count * 4) + 1];
            byte[] actualBytes = new byte[expectedBytes.Length];
            ushort[] actualWords = new ushort[expectedWords.Length];
            expectedBytes[^1] = actualBytes[^1] = byte.MaxValue;
            expectedWords[^1] = actualWords[^1] = ushort.MaxValue;

            for (int i = 0; i < count * 2; i++)
            {
                for (int channel = 0; channel < 4; channel++)
                {
                    expectedBytes[(i * 4) + channel] = bytes[channel][i];
                    if (i < count)
                    {
                        expectedWords[(i * 4) + channel] = planes[channel][i + 1];
                    }
                }
            }

            if (count == 16)
            {
                SimdUtils.Interleave4(
                    Vector256.LoadUnsafe(ref bytes[0][0]),
                    Vector256.LoadUnsafe(ref bytes[1][0]),
                    Vector256.LoadUnsafe(ref bytes[2][0]),
                    Vector256.LoadUnsafe(ref bytes[3][0]),
                    ref actualBytes[0]);

                SimdUtils.Interleave4(
                    Vector256.LoadUnsafe(ref planes[0][1]),
                    Vector256.LoadUnsafe(ref planes[1][1]),
                    Vector256.LoadUnsafe(ref planes[2][1]),
                    Vector256.LoadUnsafe(ref planes[3][1]),
                    ref actualWords[0]);
            }
            else
            {
                SimdUtils.Interleave4(
                    Vector512.LoadUnsafe(ref bytes[0][0]),
                    Vector512.LoadUnsafe(ref bytes[1][0]),
                    Vector512.LoadUnsafe(ref bytes[2][0]),
                    Vector512.LoadUnsafe(ref bytes[3][0]),
                    ref actualBytes[0]);

                SimdUtils.Interleave4(
                    Vector512.LoadUnsafe(ref planes[0][1]),
                    Vector512.LoadUnsafe(ref planes[1][1]),
                    Vector512.LoadUnsafe(ref planes[2][1]),
                    Vector512.LoadUnsafe(ref planes[3][1]),
                    ref actualWords[0]);
            }

            Assert.Equal(expectedBytes, actualBytes);
            Assert.Equal(expectedWords, actualWords);
        }
    }

    /// <summary>
    /// Verifies every packing operation against its scalar definition with and without hardware intrinsics.
    /// </summary>
    [Fact]
    public void PackingOperationsMatchScalarDefinitions()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            ValidatePackingOperations,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Exercises every SIMD transition and scalar remainder for the packing operations.
    /// </summary>
    private static void ValidatePackingOperations()
    {
        foreach (int length in Lengths)
        {
            ValidatePackedNormalizeInterleave3(length);
            ValidateUnpackDeinterleave3(length);
            ValidatePackedNormalizeInterleave4(length);
            ValidatePackedInvertNormalizeInterleave4(length);
        }
    }

    /// <summary>
    /// Compares normalized three-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedNormalizeInterleave3(int length)
    {
        const float scale = 1F / 255F;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] expected = new float[length * 3];
        float[] actual = new float[length * 3];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 3;
            expected[packedOffset] = x[i] * scale;
            expected[packedOffset + 1] = y[i] * scale;
            expected[packedOffset + 2] = z[i] * scale;
        }

        SimdUtils.PackedNormalizeInterleave3(x, y, z, actual, scale);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Compares packed three-channel deinterleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of packed values.</param>
    private static void ValidateUnpackDeinterleave3(int length)
    {
        Vector3[] packed = new Vector3[length];
        float[] expectedX = CreateSamples(length, 1);
        float[] expectedY = CreateSamples(length, 2);
        float[] expectedZ = CreateSamples(length, 3);
        float[] actualX = new float[length];
        float[] actualY = new float[length];
        float[] actualZ = new float[length];

        for (int i = 0; i < length; i++)
        {
            packed[i] = new Vector3(expectedX[i], expectedY[i], expectedZ[i]);
        }

        SimdUtils.UnpackDeinterleave3(packed, actualX, actualY, actualZ);

        Assert.Equal(expectedX, actualX);
        Assert.Equal(expectedY, actualY);
        Assert.Equal(expectedZ, actualZ);
    }

    /// <summary>
    /// Compares normalized four-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedNormalizeInterleave4(int length)
    {
        const float maximumValue = 255F;
        const float scale = 1F / maximumValue;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] w = CreateSamples(length, 4);
        float[] expected = new float[length * 4];
        float[] actual = new float[length * 4];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 4;
            expected[packedOffset] = x[i] * scale;
            expected[packedOffset + 1] = y[i] * scale;
            expected[packedOffset + 2] = z[i] * scale;
            expected[packedOffset + 3] = w[i] * scale;
        }

        SimdUtils.PackedNormalizeInterleave4(x, y, z, w, actual, maximumValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Compares inverted normalized four-plane interleaving with the original scalar loop.
    /// </summary>
    /// <param name="length">The number of samples in each component plane.</param>
    private static void ValidatePackedInvertNormalizeInterleave4(int length)
    {
        const float maximumValue = 255F;
        const float scale = 1F / maximumValue;
        float[] x = CreateSamples(length, 1);
        float[] y = CreateSamples(length, 2);
        float[] z = CreateSamples(length, 3);
        float[] w = CreateSamples(length, 4);
        float[] expected = new float[length * 4];
        float[] actual = new float[length * 4];

        for (int i = 0; i < length; i++)
        {
            int packedOffset = i * 4;
            expected[packedOffset] = (maximumValue - x[i]) * scale;
            expected[packedOffset + 1] = (maximumValue - y[i]) * scale;
            expected[packedOffset + 2] = (maximumValue - z[i]) * scale;
            expected[packedOffset + 3] = (maximumValue - w[i]) * scale;
        }

        SimdUtils.PackedInvertNormalizeInterleave4(x, y, z, w, actual, maximumValue);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Repeats explicit endpoint and fractional samples across the SIMD boundaries.
    /// </summary>
    /// <param name="length">The number of samples to create.</param>
    /// <param name="component">The one-based offset that gives each plane a different sequence.</param>
    /// <returns>The generated sample values.</returns>
    private static float[] CreateSamples(int length, int component)
    {
        // Seventeen mixed values avoid repeating at any 4-, 8-, or 16-lane boundary.
        ReadOnlySpan<float> source =
        [
            0F,
            255F,
            0.125F,
            127.5F,
            1F,
            254.875F,
            32.25F,
            96.5F,
            191.75F,
            7.125F,
            240F,
            19.5F,
            63.875F,
            128F,
            223.25F,
            48.5F,
            175.125F
        ];

        float[] samples = new float[length];

        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = source[(i + component) % source.Length];
        }

        return samples;
    }
}
