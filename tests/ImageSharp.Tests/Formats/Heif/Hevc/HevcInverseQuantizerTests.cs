// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC inverse quantization, scaling-list expansion, clipping, and SIMD behavior.
/// </summary>
[Trait("Format", "Heic")]
public class HevcInverseQuantizerTests
{
    /// <summary>
    /// Verifies fixed flat-scale results including signed rounding and both transform-range limits.
    /// </summary>
    [Fact]
    public void FlatScaleMatchesFixedResults()
    {
        int[] quantized = [1, -1, 2, -2, int.MaxValue, int.MinValue, 3, -3, 0, 0, 0, 0, 0, 0, 0, 0];
        int[] actual = new int[quantized.Length];
        HevcScalingList scalingList = new();

        HevcInverseQuantizer.Dequantize(quantized, actual, 2, 8, 15, 0, false, scalingList, HevcPlane.Y, true, false, false);

        int[] expected = [20, -20, 40, -40, 32767, -32768, 60, -60, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.True(expected.AsSpan().SequenceEqual(actual));
    }

    /// <summary>
    /// Verifies fixed nonuniform values from the default eight-by-eight intra-luma scaling matrix.
    /// </summary>
    [Fact]
    public void DefaultEightByEightScalingMatrixMatchesFixedResults()
    {
        int[] quantized = new int[64];
        quantized.AsSpan().Fill(1);
        int[] actual = new int[quantized.Length];
        HevcScalingList scalingList = new();

        HevcInverseQuantizer.Dequantize(quantized, actual, 3, 8, 15, 0, true, scalingList, HevcPlane.Y, true, false, false);

        Assert.Equal(10, actual[0]);
        Assert.Equal(11, actual[4]);
        Assert.Equal(13, actual[6]);
        Assert.Equal(72, actual[63]);
    }

    /// <summary>
    /// Compares SIMD inverse quantization with a scalar oracle across transform sizes, precisions, scaling modes, and shift directions.
    /// </summary>
    /// <param name="log2Size">The base-two logarithm of the tested transform-block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="quantizationParameter">The effective component quantization parameter.</param>
    /// <param name="scalingListEnabled">Whether scaling lists are enabled.</param>
    /// <param name="transformSkip">Whether the transform is skipped.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether transform-skip precision is extended.</param>
    [Theory]
    [InlineData(2, 8, 15, 0, false, false, false)]
    [InlineData(2, 8, 15, 27, true, false, false)]
    [InlineData(2, 10, 15, 39, true, true, false)]
    [InlineData(2, 12, 18, 51, true, true, true)]
    [InlineData(3, 10, 15, 45, true, false, false)]
    [InlineData(3, 10, 15, 45, true, true, false)]
    [InlineData(4, 12, 18, 63, true, false, false)]
    [InlineData(5, 12, 18, 75, true, false, false)]
    [InlineData(5, 12, 15, 51, false, false, false)]
    public void DequantizeMatchesScalarOracle(
        int log2Size,
        int bitDepth,
        int maxTransformDynamicRange,
        int quantizationParameter,
        bool scalingListEnabled,
        bool transformSkip,
        bool extendedPrecisionProcessingEnabled)
    {
        int size = 1 << log2Size;
        int[] quantized = new int[size * size];
        for (int i = 0; i < quantized.Length; i++)
        {
            quantized[i] = (((i * 7919) + (size * 257)) & 131071) - 65536;
        }

        HevcScalingList scalingList = new();
        int[] expected = new int[quantized.Length];
        int[] actual = new int[quantized.Length];
        DequantizeScalar(
            quantized,
            expected,
            log2Size,
            bitDepth,
            maxTransformDynamicRange,
            quantizationParameter,
            scalingListEnabled,
            scalingList,
            HevcPlane.Y,
            true,
            transformSkip,
            extendedPrecisionProcessingEnabled);

        HevcInverseQuantizer.Dequantize(
            quantized,
            actual,
            log2Size,
            bitDepth,
            maxTransformDynamicRange,
            quantizationParameter,
            scalingListEnabled,
            scalingList,
            HevcPlane.Y,
            true,
            transformSkip,
            extendedPrecisionProcessingEnabled);

        Assert.True(expected.AsSpan().SequenceEqual(actual), $"The {size}x{size} inverse quantizer did not match the scalar HEVC oracle.");
    }

    /// <summary>
    /// Applies the HEVC inverse-quantization equations directly for one complete transform block.
    /// </summary>
    /// <param name="source">The quantized coefficients.</param>
    /// <param name="destination">The dequantized coefficients.</param>
    /// <param name="log2Size">The base-two logarithm of the transform-block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="quantizationParameter">The effective component quantization parameter.</param>
    /// <param name="scalingListEnabled">Whether the sequence enables scaling matrices.</param>
    /// <param name="scalingList">The effective picture scaling matrices.</param>
    /// <param name="plane">The reconstructed color plane.</param>
    /// <param name="isIntraPredicted">Whether the transform block belongs to an intra-predicted coding unit.</param>
    /// <param name="transformSkip">Whether the transform is skipped.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether transform-skip precision is extended.</param>
    private static void DequantizeScalar(
        ReadOnlySpan<int> source,
        Span<int> destination,
        int log2Size,
        int bitDepth,
        int maxTransformDynamicRange,
        int quantizationParameter,
        bool scalingListEnabled,
        HevcScalingList scalingList,
        HevcPlane plane,
        bool isIntraPredicted,
        bool transformSkip,
        bool extendedPrecisionProcessingEnabled)
    {
        ReadOnlySpan<byte> inverseScales = [40, 45, 51, 57, 64, 72];
        int size = 1 << log2Size;
        int transformShift = maxTransformDynamicRange - bitDepth - log2Size;
        if (transformSkip && extendedPrecisionProcessingEnabled)
        {
            transformShift = Math.Max(0, transformShift);
        }

        int inverseScale = inverseScales[quantizationParameter % 6];
        bool useScalingList = scalingListEnabled && (!transformSkip || log2Size == 2);
        int rightShift = 6 - (transformShift + (quantizationParameter / 6)) + (useScalingList ? 4 : 0);
        int targetInputBitDepth = Math.Min(maxTransformDynamicRange + 1, 32 + rightShift - (useScalingList ? 15 : 7));
        int inputMinimum = -(1 << (targetInputBitDepth - 1));
        int inputMaximum = (1 << (targetInputBitDepth - 1)) - 1;
        int outputMinimum = -(1 << maxTransformDynamicRange);
        int outputMaximum = (1 << maxTransformDynamicRange) - 1;
        int sizeId = log2Size - 2;
        int matrixId = (isIntraPredicted ? 0 : 3) + (int)plane;
        ReadOnlySpan<byte> matrix = scalingList.GetMatrix(sizeId, matrixId);
        int ratio = Math.Max(1, size >> 3);
        int matrixSide = Math.Min(size, 8);
        byte dcCoefficient = scalingList.GetDcCoefficient(sizeId, matrixId);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int coefficient = useScalingList
                    ? ratio > 1 && x == 0 && y == 0
                        ? dcCoefficient
                        : matrix[((y / ratio) * matrixSide) + (x / ratio)]
                    : 1;

                int value = Math.Clamp(source[(y * size) + x], inputMinimum, inputMaximum) * inverseScale * coefficient;
                value = rightShift > 0 ? (value + (1 << (rightShift - 1))) >> rightShift : value << -rightShift;
                destination[(y * size) + x] = Math.Clamp(value, outputMinimum, outputMaximum);
            }
        }
    }
}
