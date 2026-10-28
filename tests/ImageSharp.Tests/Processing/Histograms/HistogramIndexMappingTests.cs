// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Processing.Processors.Histograms;
using SixLabors.ImageSharp.Processing.Processors.Normalization;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Processing.Histograms;

/// <summary>
/// Checks the fixed histogram mapping against exponent intervals calculated independently with double arithmetic.
/// </summary>
public class HistogramIndexMappingTests
{
    // Binary32 magnitudes span 277 doubling intervals, beginning at 2^-149. Each interval has 128 bins.
    private const int BinsPerInterval = 128;
    private const int ZeroIndex = 277 * BinsPerInterval;

    /// <summary>
    /// Checks finite Half values, float bucket boundaries and scalar remainders under every SIMD feature setting.
    /// </summary>
    [Fact]
    public void ExtendedMapping_MatchesMathematicalIntervals() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckExtendedMapping,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks bounded and extended RGB measurement traversal without changing source components or alpha.
    /// </summary>
    [Fact]
    public void RowMapping_PreservesSourceAndMatchesScalar() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckRowMapping,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks that RGB means remain finite at the largest finite channel values.
    /// </summary>
    [Fact]
    public void RgbMean_DoesNotOverflow() => FeatureTestRunner.RunWithHwIntrinsicsFeature(
        CheckRgbMean,
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Checks a full 512-bit group and three remaining samples using caller-owned memory.
    /// </summary>
    private static void CheckRgbMean()
    {
        // Nineteen samples exercise sixteen SIMD lanes followed by a three-sample remainder.
        Vector4[] source = new Vector4[19];
        source.AsSpan().Fill(new Vector4(6F, 3F, 0F, 1F));
        source[0] = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, 1F);
        source[1] = new Vector4(float.MinValue, float.MinValue, float.MinValue, 1F);
        source[^1] = new Vector4(1F, -1F, 0F, 1F);
        HistogramIndexMapping mapping = new(0, 7);
        Configuration configuration = Configuration.Default;
        using Histogram<Histogram.CountBin> histogram = new(configuration, mapping, source.Length);
        Histogram<Histogram.CountBin>.Data buffer = histogram.GetData();
        ReadOnlySpan<int> indices = buffer.Map(source, new Vector3(1F / 3F));

        Assert.Equal(ReferenceIndex(float.MaxValue), indices[0]);
        Assert.Equal(ReferenceIndex(float.MinValue), indices[1]);
        Assert.Equal(ReferenceIndex(0F), indices[^1]);

        // Every remaining color has the exact arithmetic mean three.
        for (int i = 2; i < source.Length - 1; i++)
        {
            Assert.Equal(ReferenceIndex(3F), indices[i]);
        }
    }

    /// <summary>
    /// Exercises exact boundaries with an independent mathematical index calculation.
    /// </summary>
    private static void CheckExtendedMapping()
    {
        List<float> samples = [];
        for (int bits = 0; bits <= ushort.MaxValue; bits++)
        {
            Half value = BitConverter.UInt16BitsToHalf((ushort)bits);
            if (Half.IsFinite(value))
            {
                samples.Add((float)value);
            }
        }

        // Include every bin edge across binary32, including subnormals, plus the adjacent representable values.
        for (int exponent = -149; exponent <= 127; exponent++)
        {
            for (int fraction = 0; fraction < BinsPerInterval; fraction++)
            {
                float edge = MathF.ScaleB(1F + (fraction / (float)BinsPerInterval), exponent);
                samples.Add(edge);
                samples.Add(-edge);
                samples.Add(MathF.BitIncrement(edge));
                samples.Add(MathF.BitDecrement(edge));
            }
        }

        samples.Add(float.MaxValue);
        samples.Add(float.MinValue);
        HistogramIndexMapping mapping = new(0, 7);
        float[] source = samples.ToArray();
        int[] actual = new int[source.Length];
        int[] expected = source.Select(ReferenceIndex).ToArray();
        mapping.Invoke(source, actual);
        Assert.Equal(expected, actual);

        for (int i = 0; i < source.Length; i++)
        {
            Assert.Equal(expected[i], mapping.Invoke(source[i]));
        }

        // Short lengths force each smaller register width and the final scalar tail.
        ReadOnlySpan<int> lengths = [0, 1, 3, 4, 5, 7, 8, 9, 15, 16, 17, 31, 33];
        foreach (int length in lengths)
        {
            mapping.Invoke(source.AsSpan(0, length), actual.AsSpan(0, length));
            Assert.Equal(expected.AsSpan(0, length).ToArray(), actual.AsSpan(0, length).ToArray());
        }

        Assert.Equal(ZeroIndex, mapping.Invoke(-0F));
        Assert.Equal(ZeroIndex, mapping.Invoke(0F));
        Assert.Equal(ZeroIndex - 1, mapping.Invoke(-float.Epsilon));
        Assert.Equal(ZeroIndex + 1, mapping.Invoke(float.Epsilon));
        Assert.Equal(0, mapping.Invoke(float.MinValue));
        Assert.Equal(2 * ZeroIndex, mapping.Invoke(float.MaxValue));
    }

    /// <summary>
    /// Checks fused RGB measurement with each mapping's supported range and a non-vector-length row.
    /// </summary>
    private static void CheckRowMapping()
    {
        ReadOnlySpan<Vector3> coefficients = [new(.25F, .5F, .25F), new(ColorNumerics.Bt709.X, ColorNumerics.Bt709.Y, ColorNumerics.Bt709.Z)];
        foreach (HistogramIndexMapping mapping in new HistogramIndexMapping[] { new(256, -1), new(0, 7) })
        {
            // SDR input follows the pixel API's finite [0,1] contract. Extended input also covers signed and HDR values.
            ReadOnlySpan<float> values = mapping.IsExtended
                ? [-65504F, -1F, -float.Epsilon, -0F, 0F, float.Epsilon, .125F, 1F, 16F, 65504F]
                : [0F, .125F, .25F, .5F, .75F, 1F];

            Vector4[] source = new Vector4[33];
            for (int i = 0; i < source.Length; i++)
            {
                // Distinct channels detect an incorrect transpose. Alpha is outside the measured RGB components.
                source[i] = new Vector4(values[i % values.Length], values[(i + 1) % values.Length], values[(i + 2) % values.Length], -3F);
            }

            if (mapping.IsExtended)
            {
                // Exercise the full finite float range, including the row's scalar remainder.
                source[0] = new Vector4(float.MaxValue, float.MaxValue, float.MaxValue, -3F);
                source[^1] = new Vector4(float.MinValue, float.MinValue, float.MinValue, -3F);
            }

            int[] sourceBits = MemoryMarshal.Cast<Vector4, int>(source).ToArray();
            foreach (Vector3 weights in coefficients)
            {
                int[] actual = new int[source.Length];
                mapping.Invoke(source, weights, actual);
                for (int i = 0; i < source.Length; i++)
                {
                    Vector4 value = source[i];
                    float measured = ((value.X * weights.X) + (value.Y * weights.Y)) + (value.Z * weights.Z);
                    Assert.True(float.IsFinite(measured));
                    Assert.Equal(mapping.Invoke(measured), actual[i]);
                }
            }

            Assert.Equal(sourceBits, MemoryMarshal.Cast<Vector4, int>(source).ToArray());
        }
    }

    /// <summary>
    /// Calculates the specified intervals without reading float exponent or fraction bits.
    /// </summary>
    /// <param name="value">The finite source measurement.</param>
    /// <returns>The independently calculated index.</returns>
    private static int ReferenceIndex(float value)
    {
        if (value == 0F)
        {
            return ZeroIndex;
        }

        double magnitude = Math.Abs((double)value);
        int exponent = Math.ILogB(magnitude);
        double fraction = Math.ScaleB(magnitude, -exponent) - 1D;
        int offset = ((exponent + 149) * BinsPerInterval) + (int)Math.Floor(fraction * BinsPerInterval) + 1;
        return value < 0F ? ZeroIndex - offset : ZeroIndex + offset;
    }
}
