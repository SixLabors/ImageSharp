// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Hevc;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Hevc;

/// <summary>
/// Verifies HEVC inverse-DCT, inverse-DST, rectangular-block, dynamic-range, and reconstruction behavior.
/// </summary>
[Trait("Format", "Heic")]
public class HevcInverseTransformerTests
{
    /// <summary>
    /// Verifies the two normative normalization stages with a pure DC coefficient and a strided prediction block.
    /// </summary>
    [Fact]
    public void DctDcCoefficientProducesUniformReconstruction()
    {
        const int size = 4;
        const int stride = 7;
        int[] coefficients = new int[size * size];
        coefficients[0] = 1024;
        ushort[] destination = new ushort[stride * size];
        destination.AsSpan().Fill(100);
        int[] scratch = new int[HevcInverseTransformer.GetScratchLength(2, 2)];

        HevcInverseTransformer.TransformAdd(coefficients, destination, stride, 2, 2, 8, 15, false, scratch);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Assert.Equal((ushort)108, destination[(y * stride) + x]);
            }
        }
    }

    /// <summary>
    /// Verifies a hand-derived four-by-four inverse-DST result from a pure lowest-frequency coefficient.
    /// </summary>
    [Fact]
    public void DstLowestFrequencyMatchesFixedResult()
    {
        int[] coefficients = new int[16];
        coefficients[0] = 1024;
        int[] actual = new int[16];
        int[] scratch = new int[HevcInverseTransformer.GetScratchLength(2, 2)];
        int[] expected =
        [
            2, 3, 4, 5,
            3, 6, 8, 9,
            4, 8, 11, 12,
            5, 9, 12, 14
        ];

        HevcInverseTransformer.Transform(coefficients, actual, 2, 2, 8, 15, true, scratch);

        Assert.True(expected.AsSpan().SequenceEqual(actual));
    }

    /// <summary>
    /// Compares factorized SIMD transforms with a dense specification-shaped oracle across supported dimensions and precisions.
    /// </summary>
    /// <param name="log2Width">The base-two logarithm of the tested block width.</param>
    /// <param name="log2Height">The base-two logarithm of the tested block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="useDiscreteSineTransform">Whether the tested four-by-four block uses the inverse DST.</param>
    [Theory]
    [InlineData(2, 2, 8, 15, false)]
    [InlineData(2, 2, 8, 15, true)]
    [InlineData(2, 3, 10, 15, false)]
    [InlineData(3, 2, 10, 15, false)]
    [InlineData(3, 3, 10, 15, false)]
    [InlineData(4, 3, 12, 18, false)]
    [InlineData(3, 4, 12, 18, false)]
    [InlineData(5, 4, 12, 18, false)]
    [InlineData(4, 5, 12, 18, false)]
    [InlineData(5, 5, 12, 15, false)]
    public void TransformMatchesDenseOracle(
        int log2Width,
        int log2Height,
        int bitDepth,
        int maxTransformDynamicRange,
        bool useDiscreteSineTransform)
    {
        int width = 1 << log2Width;
        int height = 1 << log2Height;
        int sampleCount = width * height;
        int[] coefficients = new int[sampleCount];
        int coefficientMaximum = (1 << maxTransformDynamicRange) - 1;
        for (int i = 0; i < coefficients.Length; i++)
        {
            // Alternating values across the complete dequantized range exercise both intermediate clipping bounds
            // as well as every frequency group used by the factorized transform.
            int unit = (((i * 37) + (width * 11) + height) % 127) - 63;
            coefficients[i] = (unit * coefficientMaximum) / 63;
        }

        int[] expected = new int[sampleCount];
        int[] actual = new int[sampleCount];
        int[] oracleScratch = new int[sampleCount];
        int[] transformScratch = new int[HevcInverseTransformer.GetScratchLength(log2Width, log2Height)];
        TransformDenseOracle(
            coefficients,
            expected,
            oracleScratch,
            width,
            height,
            bitDepth,
            maxTransformDynamicRange,
            useDiscreteSineTransform);

        HevcInverseTransformer.Transform(
            coefficients,
            actual,
            log2Width,
            log2Height,
            bitDepth,
            maxTransformDynamicRange,
            useDiscreteSineTransform,
            transformScratch);

        Assert.True(expected.AsSpan().SequenceEqual(actual), $"The {width}x{height} transform at {bitDepth} bits did not match the dense HEVC oracle.");
    }

    /// <summary>
    /// Verifies that residual addition clips both negative and positive reconstruction overflow at the component range.
    /// </summary>
    [Fact]
    public void TransformAddClipsToComponentRange()
    {
        const int size = 4;
        int[] positiveCoefficients = new int[size * size];
        int[] negativeCoefficients = new int[size * size];
        positiveCoefficients[0] = 32767;
        negativeCoefficients[0] = -32768;
        ushort[] positive = new ushort[size * size];
        ushort[] negative = new ushort[size * size];
        positive.AsSpan().Fill(4090);
        negative.AsSpan().Fill(5);
        int[] scratch = new int[HevcInverseTransformer.GetScratchLength(2, 2)];

        HevcInverseTransformer.TransformAdd(positiveCoefficients, positive, size, 2, 2, 12, 18, false, scratch);
        HevcInverseTransformer.TransformAdd(negativeCoefficients, negative, size, 2, 2, 12, 18, false, scratch);

        Assert.All(positive, value => Assert.Equal((ushort)4095, value));
        Assert.All(negative, value => Assert.Equal((ushort)0, value));
    }

    /// <summary>
    /// Applies both inverse-transform dimensions using direct matrix products and normative rounding points.
    /// </summary>
    /// <param name="coefficients">The dequantized coefficient block.</param>
    /// <param name="residual">The destination signed residual block.</param>
    /// <param name="intermediate">The full-block intermediate buffer.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="useDiscreteSineTransform">Whether both dimensions use the four-point inverse DST.</param>
    private static void TransformDenseOracle(
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        Span<int> intermediate,
        int width,
        int height,
        int bitDepth,
        int maxTransformDynamicRange,
        bool useDiscreteSineTransform)
    {
        int dynamicMinimum = -(1 << maxTransformDynamicRange);
        int dynamicMaximum = (1 << maxTransformDynamicRange) - 1;
        for (int y = 0; y < height; y++)
        {
            for (int xFrequency = 0; xFrequency < width; xFrequency++)
            {
                int sum = 0;
                for (int yFrequency = 0; yFrequency < height; yFrequency++)
                {
                    sum += coefficients[(yFrequency * width) + xFrequency] * GetInverseCoefficient(height, yFrequency, y, useDiscreteSineTransform);
                }

                intermediate[(y * width) + xFrequency] = Math.Clamp((sum + 64) >> 7, dynamicMinimum, dynamicMaximum);
            }
        }

        int secondShift = maxTransformDynamicRange + 5 - bitDepth;
        int secondRounding = 1 << (secondShift - 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sum = 0;
                for (int xFrequency = 0; xFrequency < width; xFrequency++)
                {
                    sum += intermediate[(y * width) + xFrequency] * GetInverseCoefficient(width, xFrequency, x, useDiscreteSineTransform);
                }

                residual[(y * width) + x] = Math.Clamp((sum + secondRounding) >> secondShift, short.MinValue, short.MaxValue);
            }
        }
    }

    /// <summary>
    /// Gets one coefficient from the normative HEVC inverse-DCT or four-point inverse-DST matrix.
    /// </summary>
    /// <param name="size">The transform dimension.</param>
    /// <param name="frequency">The frequency-domain coordinate.</param>
    /// <param name="position">The spatial-domain coordinate.</param>
    /// <param name="useDiscreteSineTransform">Whether the four-point inverse DST is selected.</param>
    /// <returns>The signed matrix coefficient.</returns>
    private static int GetInverseCoefficient(int size, int frequency, int position, bool useDiscreteSineTransform)
    {
        if (useDiscreteSineTransform)
        {
            ReadOnlySpan<sbyte> sine =
            [
                29, 55, 74, 84,
                74, 74, 0, -74,
                84, -29, -74, 55,
                55, -84, 74, -29
            ];

            return sine[(frequency * 4) + position];
        }

        if (frequency == 0)
        {
            return 64;
        }

        ReadOnlySpan<sbyte> magnitudes =
        [
            90, 90, 90, 90, 89, 88, 87, 85, 83, 82, 80, 78, 75, 73, 70, 67, 64,
            61, 57, 54, 50, 46, 43, 38, 36, 31, 25, 22, 18, 13, 9, 4, 0
        ];

        int angle = (((2 * position) + 1) * frequency * (32 / size)) & 127;
        if (angle > 64)
        {
            angle = 128 - angle;
        }

        return angle > 32 ? -magnitudes[64 - angle] : magnitudes[angle];
    }
}
