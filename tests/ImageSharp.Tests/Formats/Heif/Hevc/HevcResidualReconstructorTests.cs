// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC transform-skip, transquant-bypass, rotation, and residual differential reconstruction.
/// </summary>
[Trait("Format", "Heic")]
public class HevcResidualReconstructorTests
{
    /// <summary>
    /// Verifies that lossless transquant bypass preserves or completely reverses coefficient order.
    /// </summary>
    /// <param name="rotate">Whether the coefficient order is reversed.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyBypassedPreservesOrRotatesCoefficientOrder(bool rotate)
    {
        int[] coefficients = new int[1024];
        int[] actual = new int[coefficients.Length];
        int[] expected = new int[coefficients.Length];
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = (i * 17) - 8000;
        }

        for (int i = 0; i < coefficients.Length; i++)
        {
            expected[i] = rotate ? coefficients[coefficients.Length - 1 - i] : coefficients[i];
        }

        HevcResidualReconstructor.CopyBypassed(coefficients, actual, rotate);

        int mismatch = expected.AsSpan().SequenceEqual(actual) ? -1 : FindFirstMismatch(expected, actual);
        Assert.True(mismatch < 0, mismatch < 0 ? string.Empty : $"Mismatch at {mismatch}: expected {expected[mismatch]}, actual {actual[mismatch]}.");
    }

    /// <summary>
    /// Compares SIMD transform-skip reconstruction with a scalar oracle across transform sizes and signed shift directions.
    /// </summary>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="equivalentLog2TransformSize">The base-two logarithm of the equivalent square transform size.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether extended transform-skip precision applies.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    [Theory]
    [InlineData(4, 4, 8, 15, 2, false, true)]
    [InlineData(4, 8, 8, 15, 3, false, false)]
    [InlineData(8, 4, 10, 15, 2, false, false)]
    [InlineData(8, 8, 10, 15, 3, false, false)]
    [InlineData(16, 16, 12, 15, 4, false, false)]
    [InlineData(16, 16, 12, 15, 4, true, false)]
    [InlineData(32, 32, 12, 18, 5, false, false)]
    public void TransformSkipMatchesScalarOracle(
        int width,
        int height,
        int bitDepth,
        int maxTransformDynamicRange,
        int equivalentLog2TransformSize,
        bool extendedPrecisionProcessingEnabled,
        bool rotate)
    {
        int coefficientCount = width * height;
        int[] coefficients = new int[coefficientCount];
        int[] actual = new int[coefficientCount];
        int[] expected = new int[coefficientCount];
        for (int i = 0; i < coefficientCount; i++)
        {
            coefficients[i] = (((i * 7919) + (width * 257)) & 65535) - 32768;
        }

        ApplyTransformSkipScalar(
            coefficients,
            expected,
            bitDepth,
            maxTransformDynamicRange,
            equivalentLog2TransformSize,
            extendedPrecisionProcessingEnabled,
            rotate);

        HevcResidualReconstructor.ApplyTransformSkip(
            coefficients,
            actual,
            width,
            height,
            bitDepth,
            maxTransformDynamicRange,
            equivalentLog2TransformSize,
            extendedPrecisionProcessingEnabled,
            rotate);

        Assert.True(expected.AsSpan().SequenceEqual(actual));
    }

    /// <summary>
    /// Compares SIMD residual differential reconstruction with the sequential normative recurrence.
    /// </summary>
    /// <param name="size">The square residual-block side.</param>
    /// <param name="modeValue">The numeric differential accumulation direction.</param>
    [Theory]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(8, 1)]
    [InlineData(8, 2)]
    [InlineData(16, 1)]
    [InlineData(16, 2)]
    [InlineData(32, 1)]
    [InlineData(32, 2)]
    public void ResidualDpcmMatchesScalarOracle(int size, int modeValue)
    {
        HevcResidualDpcmMode mode = (HevcResidualDpcmMode)modeValue;
        int[] actual = new int[size * size];
        for (int i = 0; i < actual.Length; i++)
        {
            actual[i] = (((i * 104729) + (size * 4099)) & 8191) - 4096;
        }

        int[] expected = (int[])actual.Clone();
        ApplyResidualDpcmScalar(expected, size, size, mode);

        HevcResidualReconstructor.ApplyResidualDpcm(actual, size, size, mode);

        Assert.True(expected.AsSpan().SequenceEqual(actual));
    }

    /// <summary>
    /// Verifies signed residual clipping without clipping the thirty-two-bit recurrence accumulator.
    /// </summary>
    /// <param name="modeValue">The numeric differential accumulation direction.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ResidualDpcmClipsStoredSamples(int modeValue)
    {
        HevcResidualDpcmMode mode = (HevcResidualDpcmMode)modeValue;
        int[] actual = new int[32 * 32];
        actual.AsSpan().Fill(3000);
        int[] expected = (int[])actual.Clone();
        ApplyResidualDpcmScalar(expected, 32, 32, mode);

        HevcResidualReconstructor.ApplyResidualDpcm(actual, 32, 32, mode);

        Assert.True(expected.AsSpan().SequenceEqual(actual));
        Assert.Contains(short.MaxValue, actual);
    }

    /// <summary>
    /// Verifies the Range Extensions rotation constraint for non-transformed intra blocks.
    /// </summary>
    [Fact]
    public void RotationRequiresEnabledFourWideIntraBlock()
    {
        Assert.True(HevcResidualReconstructor.IsNonTransformedResidualRotated(true, true, 4));
        Assert.False(HevcResidualReconstructor.IsNonTransformedResidualRotated(false, true, 4));
        Assert.False(HevcResidualReconstructor.IsNonTransformedResidualRotated(true, false, 4));
        Assert.False(HevcResidualReconstructor.IsNonTransformedResidualRotated(true, true, 8));
    }

    /// <summary>
    /// Verifies implicit residual differential mode selection, including 4:2:2 chroma angle remapping.
    /// </summary>
    [Fact]
    public void ImplicitResidualDpcmFollowsPredictionDirection()
    {
        Assert.Equal(HevcResidualDpcmMode.Horizontal, HevcResidualReconstructor.GetImplicitResidualDpcmMode(10, false));
        Assert.Equal(HevcResidualDpcmMode.Vertical, HevcResidualReconstructor.GetImplicitResidualDpcmMode(26, false));
        Assert.Equal(HevcResidualDpcmMode.None, HevcResidualReconstructor.GetImplicitResidualDpcmMode(18, false));
        Assert.Equal(HevcResidualDpcmMode.Horizontal, HevcResidualReconstructor.GetImplicitResidualDpcmMode(10, true));
        Assert.Equal(HevcResidualDpcmMode.Vertical, HevcResidualReconstructor.GetImplicitResidualDpcmMode(26, true));
    }

    /// <summary>
    /// Applies the normative transform-skip normalization as a scalar test oracle.
    /// </summary>
    /// <param name="coefficients">The dequantized coefficients.</param>
    /// <param name="residual">The destination residual block.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="equivalentLog2TransformSize">The base-two logarithm of the equivalent square transform size.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether extended transform-skip precision applies.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    private static void ApplyTransformSkipScalar(
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        int bitDepth,
        int maxTransformDynamicRange,
        int equivalentLog2TransformSize,
        bool extendedPrecisionProcessingEnabled,
        bool rotate)
    {
        int shift = maxTransformDynamicRange - bitDepth - equivalentLog2TransformSize;
        if (extendedPrecisionProcessingEnabled)
        {
            shift = Math.Max(0, shift);
        }

        for (int i = 0; i < coefficients.Length; i++)
        {
            int value = coefficients[rotate ? coefficients.Length - 1 - i : i];
            residual[i] = shift > 0
                ? (value + (1 << (shift - 1))) >> shift
                : value << -shift;
        }
    }

    /// <summary>
    /// Applies the normative inverse residual differential recurrence as a scalar test oracle.
    /// </summary>
    /// <param name="residual">The residual block in packed raster order.</param>
    /// <param name="width">The residual-block width.</param>
    /// <param name="height">The residual-block height.</param>
    /// <param name="mode">The differential accumulation direction.</param>
    private static void ApplyResidualDpcmScalar(Span<int> residual, int width, int height, HevcResidualDpcmMode mode)
    {
        if (mode == HevcResidualDpcmMode.Vertical)
        {
            for (int x = 0; x < width; x++)
            {
                int accumulator = residual[x];
                for (int y = 1; y < height; y++)
                {
                    int index = (y * width) + x;
                    accumulator += residual[index];
                    residual[index] = Math.Clamp(accumulator, short.MinValue, short.MaxValue);
                }
            }
        }
        else if (mode == HevcResidualDpcmMode.Horizontal)
        {
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width;
                int accumulator = residual[rowOffset];
                for (int x = 1; x < width; x++)
                {
                    int index = rowOffset + x;
                    accumulator += residual[index];
                    residual[index] = Math.Clamp(accumulator, short.MinValue, short.MaxValue);
                }
            }
        }
    }

    /// <summary>
    /// Finds the first unequal element in two equally sized test buffers.
    /// </summary>
    /// <param name="expected">The expected values.</param>
    /// <param name="actual">The actual values.</param>
    /// <returns>The first unequal index.</returns>
    private static int FindFirstMismatch(ReadOnlySpan<int> expected, ReadOnlySpan<int> actual)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        return -1;
    }
}
