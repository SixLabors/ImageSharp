// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 forward quantization against current libaom's fast no-matrix arithmetic.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ForwardQuantizerTests
{
    /// <summary>
    /// The hardware configurations covering every quantizer vector tier and the scalar fallback.
    /// </summary>
    private const HwIntrinsics QuantizerConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies raster quantization and scan-order EOB selection at every SIMD tier.
    /// </summary>
    [Fact]
    public void FastQuantizerMatchesLibaomReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateQuantizer, QuantizerConfigurations);

    /// <summary>
    /// Verifies that repeated transform quantization uses only caller-owned buffers.
    /// </summary>
    [Fact]
    public void QuantizerDoesNotAllocatePerTransform()
    {
        const int coefficientCount = 64;
        int[] coefficients = new int[coefficientCount];
        int[] quantized = new int[coefficientCount];
        int[] dequantized = new int[coefficientCount];
        FillCoefficients(coefficients, 73);

        Av1ForwardQuantizer.QuantizeLossy(
            coefficients,
            quantized,
            dequantized,
            Av1TransformSize.Size8x8,
            Av1TransformType.DctDct,
            73,
            -1,
            3,
            Av1BitDepth.TenBit);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1ForwardQuantizer.QuantizeLossy(
                coefficients,
                quantized,
                dequantized,
                Av1TransformSize.Size8x8,
                Av1TransformType.DctDct,
                73,
                -1,
                3,
                Av1BitDepth.TenBit);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>
    /// Verifies that lossless quantization removes only the reversible transform scale.
    /// </summary>
    [Fact]
    public void LosslessQuantizerRetainsExactReconstructionCoefficients()
    {
        int[] coefficients =
        [
            4, -8, 12, -16,
            20, -24, 28, -32,
            36, -40, 44, -48,
            52, -56, 60, -64
        ];

        int[] quantized = new int[coefficients.Length];
        int[] dequantized = new int[coefficients.Length];

        ushort endOfBlock = Av1ForwardQuantizer.QuantizeLossless(
            coefficients,
            quantized,
            dequantized,
            Av1BitDepth.TwelveBit);

        Assert.Equal((ushort)16, endOfBlock);
        Assert.Equal(coefficients.Select(x => x / 4), quantized);
        Assert.Equal(coefficients, dequantized);
    }

    /// <summary>
    /// Exercises each transform-scale category, coded 64-point layout, quantizer range, and sample precision.
    /// </summary>
    private static void ValidateQuantizer()
    {
        ReadOnlySpan<Av1TransformSize> transformSizes =
        [
            Av1TransformSize.Size4x4,
            Av1TransformSize.Size8x8,
            Av1TransformSize.Size16x16,
            Av1TransformSize.Size32x32,
            Av1TransformSize.Size64x16,
            Av1TransformSize.Size64x64,
        ];

        ReadOnlySpan<int> quantizerIndices = [1, 73, 173, 255];
        ReadOnlySpan<Av1BitDepth> bitDepths = [Av1BitDepth.EightBit, Av1BitDepth.TenBit, Av1BitDepth.TwelveBit];

        foreach (Av1TransformSize transformSize in transformSizes)
        {
            int coefficientCount = transformSize.GetAdjusted().GetSize2d();
            int[] coefficients = new int[coefficientCount];
            int[] expectedQuantized = new int[coefficientCount];
            int[] expectedDequantized = new int[coefficientCount];
            int[] actualQuantized = new int[coefficientCount];
            int[] actualDequantized = new int[coefficientCount];

            foreach (int qIndex in quantizerIndices)
            {
                FillCoefficients(coefficients, qIndex);

                foreach (Av1BitDepth bitDepth in bitDepths)
                {
                    ushort expectedEndOfBlock = QuantizeReference(
                        coefficients,
                        expectedQuantized,
                        expectedDequantized,
                        transformSize,
                        Av1TransformType.DctDct,
                        qIndex,
                        -1,
                        3,
                        bitDepth);

                    ushort actualEndOfBlock = Av1ForwardQuantizer.QuantizeLossy(
                        coefficients,
                        actualQuantized,
                        actualDequantized,
                        transformSize,
                        Av1TransformType.DctDct,
                        qIndex,
                        -1,
                        3,
                        bitDepth);

                    Assert.Equal(expectedEndOfBlock, actualEndOfBlock);
                    Assert.Equal(expectedQuantized, actualQuantized);
                    Assert.Equal(expectedDequantized, actualDequantized);
                }
            }
        }
    }

    /// <summary>
    /// Fills one transform with deterministic signed values spanning threshold, rounding, and clamp behavior.
    /// </summary>
    private static void FillCoefficients(Span<int> coefficients, int seed)
    {
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = (((i * 7919) + (seed * 313)) % 90001) - 45000;
        }

        coefficients[0] = 0;
        coefficients[1] = 1;
        coefficients[2] = -1;
        coefficients[3] = short.MaxValue;
        coefficients[4] = -short.MaxValue;
    }

    /// <summary>
    /// Mirrors av1_quantize_fp_no_qmatrix from current libaom without sharing the production traversal.
    /// </summary>
    private static ushort QuantizeReference(
        ReadOnlySpan<int> coefficients,
        Span<int> quantizedCoefficients,
        Span<int> dequantizedCoefficients,
        Av1TransformSize transformSize,
        Av1TransformType transformType,
        int qIndex,
        int dcDeltaQ,
        int acDeltaQ,
        Av1BitDepth bitDepth)
    {
        quantizedCoefficients.Clear();
        dequantizedCoefficients.Clear();

        int logScale = transformSize.GetScale();
        int dcDequantizer = Av1QuantizationLookup.GetDcQuant(qIndex, dcDeltaQ, bitDepth);
        int acDequantizer = Av1QuantizationLookup.GetAcQuant(qIndex, acDeltaQ, bitDepth);
        int dcQuantizer = (1 << 16) / dcDequantizer;
        int acQuantizer = (1 << 16) / acDequantizer;
        int dcRounding = RoundPowerOfTwo((64 * dcDequantizer) >> 7, logScale);
        int acRounding = RoundPowerOfTwo((64 * acDequantizer) >> 7, logScale);
        ReadOnlySpan<short> scan = Av1ScanOrderConstants.GetScanOrder(transformSize, transformType).Scan;
        ushort endOfBlock = 0;

        for (int scanIndex = 0; scanIndex < scan.Length; scanIndex++)
        {
            int coefficientIndex = scan[scanIndex];
            int coefficient = coefficients[coefficientIndex];
            int coefficientSign = coefficient >> 31;
            long magnitude = ((long)coefficient ^ coefficientSign) - coefficientSign;
            int dequantizer = coefficientIndex == 0 ? dcDequantizer : acDequantizer;
            int quantizer = coefficientIndex == 0 ? dcQuantizer : acQuantizer;
            int rounding = coefficientIndex == 0 ? dcRounding : acRounding;
            int quantizedMagnitude = 0;

            if ((magnitude << (1 + logScale)) >= dequantizer)
            {
                magnitude += rounding;
                if (bitDepth == Av1BitDepth.EightBit)
                {
                    magnitude = Math.Min(magnitude, short.MaxValue);
                }

                quantizedMagnitude = (int)((magnitude * quantizer) >> (16 - logScale));
            }

            if (quantizedMagnitude != 0)
            {
                quantizedCoefficients[coefficientIndex] = (quantizedMagnitude ^ coefficientSign) - coefficientSign;
                int dequantizedMagnitude = (quantizedMagnitude * dequantizer) >> logScale;
                dequantizedCoefficients[coefficientIndex] = (dequantizedMagnitude ^ coefficientSign) - coefficientSign;
                endOfBlock = (ushort)(scanIndex + 1);
            }
        }

        return endOfBlock;
    }

    /// <summary>
    /// Applies libaom's positive round-power-of-two operation.
    /// </summary>
    private static int RoundPowerOfTwo(int value, int shift)
        => shift == 0 ? value : (value + (1 << (shift - 1))) >> shift;
}
