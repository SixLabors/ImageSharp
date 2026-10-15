// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 residual construction against source-minus-prediction reference arithmetic.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ResidualBuilderTests
{
    private const HwIntrinsics ResidualConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies rectangular search costs, 8-bit, 10-bit, and 12-bit residuals, residual energy, neighbor correlation,
    /// and compound search measures against their scalar definitions across hardware paths.
    /// </summary>
    [Fact]
    public void KernelsMatchScalarAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateKernels, ResidualConfigurations);

    private static void ValidateKernels()
    {
        ValidateRectangularSearchMetrics();
        ValidateResiduals();
        ValidateSumSquares();
        ValidateCorrelation();
        ValidateCompoundSearchMetrics();
        ValidateResidueOutsideFrame();
    }

    /// <summary>
    /// Compares the outside-frame residue fill with the per-sample definition of fill_residue_outside_frame() for
    /// every transform class and clipped extent.
    /// </summary>
    private static void ValidateResidueOutsideFrame()
    {
        Av1TransformType[] types =
        [
            Av1TransformType.DctDct, Av1TransformType.AdstFlipAdst, Av1TransformType.Identity,
            Av1TransformType.VerticalDct, Av1TransformType.HorizontalDct
        ];

        foreach (int columns in new[] { 4, 8, 16, 32, 64 })
        {
            foreach (int rows in new[] { 4, 8, 16, 32, 64 })
            {
                int stride = columns + 5;
                foreach (Av1TransformType type in types)
                {
                    foreach (int visibleColumns in new[] { 0, 1, columns / 2, columns - 1, columns })
                    {
                        foreach (int visibleRows in new[] { 0, 1, rows / 2, rows - 1, rows })
                        {
                            short[] expected = new short[stride * rows];
                            for (int i = 0; i < expected.Length; i++)
                            {
                                expected[i] = (short)(((i * 7919) % 8191) - 4095);
                            }

                            short[] actual = (short[])expected.Clone();
                            ReferenceFillResidueOutsideFrame(expected, stride, columns, rows, visibleColumns, visibleRows, type);
                            Av1ResidualBuilder.FillResidueOutsideFrame(actual, stride, columns, rows, visibleColumns, visibleRows, type);
                            Assert.Equal(expected, actual);
                        }
                    }
                }
            }
        }
    }

    private static void ReferenceFillResidueOutsideFrame(
        short[] residual,
        int stride,
        int columns,
        int rows,
        int visibleColumns,
        int visibleRows,
        Av1TransformType type)
    {
        static int DivideAndRoundSigned(int numerator, int denominator)
            => numerator < 0 ? (numerator - (denominator / 2)) / denominator : (numerator + (denominator / 2)) / denominator;

        bool outside = visibleColumns == 0 || visibleRows == 0;
        if (type <= Av1TransformType.Identity)
        {
            int sum = 0;
            for (int row = 0; row < visibleRows; row++)
            {
                for (int column = 0; column < visibleColumns; column++)
                {
                    sum += residual[(row * stride) + column];
                }
            }

            short average = type != Av1TransformType.Identity && !outside
                ? (short)DivideAndRoundSigned(sum, visibleColumns * visibleRows)
                : (short)0;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    if (row >= visibleRows || column >= visibleColumns)
                    {
                        residual[(row * stride) + column] = average;
                    }
                }
            }

            return;
        }

        bool horizontalIdentity = type is Av1TransformType.VerticalDct or Av1TransformType.VerticalAdst or Av1TransformType.VerticalFlipAdst;
        if (horizontalIdentity)
        {
            for (int column = 0; column < visibleColumns; column++)
            {
                int sum = 0;
                for (int row = 0; row < visibleRows; row++)
                {
                    sum += residual[(row * stride) + column];
                }

                short average = outside ? (short)0 : (short)DivideAndRoundSigned(sum, visibleRows);
                for (int row = visibleRows; row < rows; row++)
                {
                    residual[(row * stride) + column] = average;
                }
            }

            for (int row = 0; row < rows; row++)
            {
                for (int column = visibleColumns; column < columns; column++)
                {
                    residual[(row * stride) + column] = 0;
                }
            }

            return;
        }

        for (int row = 0; row < visibleRows; row++)
        {
            int sum = 0;
            for (int column = 0; column < visibleColumns; column++)
            {
                sum += residual[(row * stride) + column];
            }

            short average = outside ? (short)0 : (short)DivideAndRoundSigned(sum, visibleColumns);
            for (int column = visibleColumns; column < columns; column++)
            {
                residual[(row * stride) + column] = average;
            }
        }

        for (int row = visibleRows; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                residual[(row * stride) + column] = 0;
            }
        }
    }

    /// <summary>
    /// Exercises independent strides and exact final-row lengths, including every coded block dimension and vector tails.
    /// </summary>
    private static void ValidateRectangularSearchMetrics()
    {
        foreach (int width in new[] { 4, 8, 12, 16, 24, 31, 32, 63, 64, 127, 128 })
        {
            foreach (int height in new[] { 3, 4, 8, 16, 32, 64, 128 })
            {
                ValidateByteRectangularSearchMetrics(width, height);
                ValidateUInt16RectangularSearchMetrics(width, height, 1023);
                ValidateUInt16RectangularSearchMetrics(width, height, 4095);
            }
        }
    }

    /// <summary>
    /// Compares each metric with scalar arithmetic for mixed residuals and both signs at maximum magnitude.
    /// </summary>
    private static void ValidateByteRectangularSearchMetrics(int width, int height)
    {
        int sourceStride = width + 3;
        int predictionStride = width + 7;
        byte[] source = new byte[1 + ((height - 1) * sourceStride) + width];
        byte[] prediction = new byte[3 + ((height - 1) * predictionStride) + width];
        Span<byte> sourcePlane = source.AsSpan(1);
        Span<byte> predictionPlane = prediction.AsSpan(3);

        for (int pattern = 0; pattern < 3; pattern++)
        {
            FillBytePlanes(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height);
            int expectedSum = 0;
            long expectedSquares = 0;
            int expectedSad = 0;
            int expectedAlternateSad = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pattern != 0)
                    {
                        // Constant extrema expose overflowing signed reductions and squared block totals.
                        sourcePlane[(y * sourceStride) + x] = (byte)(pattern == 1 ? byte.MaxValue : 0);
                        predictionPlane[(y * predictionStride) + x] = (byte)(pattern == 2 ? byte.MaxValue : 0);
                    }

                    int difference = sourcePlane[(y * sourceStride) + x] - predictionPlane[(y * predictionStride) + x];
                    expectedSum += difference;
                    expectedSquares += (long)difference * difference;
                    expectedSad += Math.Abs(difference);
                    if ((y & 1) == 0)
                    {
                        expectedAlternateSad += 2 * Math.Abs(difference);
                    }
                }
            }

            Av1ResidualBuilder.GetMoments(
                sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, out int sum, out long squares);

            Assert.Equal(expectedSum, sum);
            Assert.Equal(expectedSquares, squares);
            Assert.Equal(
                expectedSad,
                Av1ResidualBuilder.SumAbsoluteDifferences(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, 1));

            Assert.Equal(
                expectedAlternateSad,
                Av1ResidualBuilder.SumAbsoluteDifferences(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, 2));
        }
    }

    /// <summary>
    /// Compares each metric with scalar arithmetic for mixed residuals and both signs at maximum magnitude.
    /// </summary>
    private static void ValidateUInt16RectangularSearchMetrics(int width, int height, int maximumSample)
    {
        int sourceStride = width + 3;
        int predictionStride = width + 7;
        ushort[] source = new ushort[1 + ((height - 1) * sourceStride) + width];
        ushort[] prediction = new ushort[3 + ((height - 1) * predictionStride) + width];
        Span<ushort> sourcePlane = source.AsSpan(1);
        Span<ushort> predictionPlane = prediction.AsSpan(3);

        for (int pattern = 0; pattern < 3; pattern++)
        {
            FillUInt16Planes(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, maximumSample);
            int expectedSum = 0;
            long expectedSquares = 0;
            int expectedSad = 0;
            int expectedAlternateSad = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pattern != 0)
                    {
                        // Constant extrema expose overflowing signed reductions and squared block totals.
                        sourcePlane[(y * sourceStride) + x] = (ushort)(pattern == 1 ? maximumSample : 0);
                        predictionPlane[(y * predictionStride) + x] = (ushort)(pattern == 2 ? maximumSample : 0);
                    }

                    int difference = sourcePlane[(y * sourceStride) + x] - predictionPlane[(y * predictionStride) + x];
                    expectedSum += difference;
                    expectedSquares += (long)difference * difference;
                    expectedSad += Math.Abs(difference);
                    if ((y & 1) == 0)
                    {
                        expectedAlternateSad += 2 * Math.Abs(difference);
                    }
                }
            }

            Av1ResidualBuilder.GetMoments(
                sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, out int sum, out long squares);

            Assert.Equal(expectedSum, sum);
            Assert.Equal(expectedSquares, squares);
            Assert.Equal(
                expectedSad,
                Av1ResidualBuilder.SumAbsoluteDifferences(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, 1));

            Assert.Equal(
                expectedAlternateSad,
                Av1ResidualBuilder.SumAbsoluteDifferences(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, 2));
        }
    }

    private static void ValidateSumSquares()
    {
        short[] residual = new short[127];
        long expected = 0;
        for (int index = 0; index < residual.Length; index++)
        {
            int magnitude = (index * 193) & 4095;
            short value = (short)((index & 1) == 0 ? magnitude : -magnitude);
            residual[index] = value;
            expected += (long)value * value;
        }

        residual[0] = -4095;
        expected += 4095L * 4095;
        Assert.Equal(expected, Av1ResidualBuilder.SumSquares(residual));

        long expectedSum = 0;
        foreach (short value in residual)
        {
            expectedSum += value;
        }

        Assert.Equal(expected, Av1ResidualBuilder.SumAndSumSquares(residual, out long actualSum));
        Assert.Equal(expectedSum, actualSum);

        // A visible rectangle of a strided plane, including widths with a scalar tail. Values outside the
        // rectangle are extreme, so any sample read past the rectangle changes the total.
        Random random = new(0x5A5A);
        foreach (int width in new[] { 1, 4, 7, 8, 13, 16, 31, 32, 33, 64, 128 })
        {
            foreach (int height in new[] { 1, 3, 8, 64 })
            {
                int stride = width + 9;
                short[] plane = new short[stride * height];
                plane.AsSpan().Fill(short.MinValue);
                long expectedRectangle = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        short value = (short)random.Next(-32768, 32768);
                        plane[(y * stride) + x] = value;
                        expectedRectangle += (long)value * value;
                    }
                }

                Assert.Equal(expectedRectangle, Av1ResidualBuilder.SumSquares(plane, stride, width, height));

                long expectedRectangleSum = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        expectedRectangleSum += plane[(y * stride) + x];
                    }
                }

                Assert.Equal(expectedRectangle, Av1ResidualBuilder.SumAndSumSquares(plane, stride, width, height, out long rectangleSum));
                Assert.Equal(expectedRectangleSum, rectangleSum);
            }
        }
    }

    private static void ValidateResiduals()
    {
        ValidateByteResiduals();
        ValidateUInt16Residuals(1023);
        ValidateUInt16Residuals(4095);
    }

    private static void ValidateByteResiduals()
    {
        const int width = 127;
        const int height = 3;
        const int sourceStride = 131;
        const int predictionStride = 137;
        const int residualStride = 139;
        const int sourceOffset = 1;
        const int predictionOffset = 2;
        const int residualOffset = 3;
        byte[] source = new byte[sourceOffset + (sourceStride * height)];
        byte[] prediction = new byte[predictionOffset + (predictionStride * height)];
        short[] expected = new short[residualOffset + (residualStride * height)];
        short[] actual = new short[expected.Length];
        Array.Fill(expected, short.MinValue);
        Array.Fill(actual, short.MinValue);

        Span<byte> sourcePlane = source.AsSpan(sourceOffset);
        Span<byte> predictionPlane = prediction.AsSpan(predictionOffset);
        Span<short> expectedPlane = expected.AsSpan(residualOffset);
        Span<short> actualPlane = actual.AsSpan(residualOffset);
        FillBytePlanes(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height);
        FillReference(sourcePlane, sourceStride, predictionPlane, predictionStride, expectedPlane, residualStride, width, height);

        Av1ResidualBuilder.Subtract(sourcePlane, sourceStride, predictionPlane, predictionStride, actualPlane, residualStride, width, height);

        Assert.Equal(expected, actual);
        long expectedSquaredError = CalculateReferenceSquaredError(expectedPlane, residualStride, width, height);

        Assert.Equal(
            expectedSquaredError,
            Av1ResidualBuilder.SumSquaredError(
                sourcePlane,
                sourceStride,
                predictionPlane,
                predictionStride,
                width,
                height));
    }

    private static void ValidateUInt16Residuals(int maximumSample)
    {
        const int width = 127;
        const int height = 3;
        const int sourceStride = 131;
        const int predictionStride = 137;
        const int residualStride = 139;
        const int sourceOffset = 1;
        const int predictionOffset = 2;
        const int residualOffset = 3;
        ushort[] source = new ushort[sourceOffset + (sourceStride * height)];
        ushort[] prediction = new ushort[predictionOffset + (predictionStride * height)];
        short[] expected = new short[residualOffset + (residualStride * height)];
        short[] actual = new short[expected.Length];
        Array.Fill(expected, short.MinValue);
        Array.Fill(actual, short.MinValue);

        Span<ushort> sourcePlane = source.AsSpan(sourceOffset);
        Span<ushort> predictionPlane = prediction.AsSpan(predictionOffset);
        Span<short> expectedPlane = expected.AsSpan(residualOffset);
        Span<short> actualPlane = actual.AsSpan(residualOffset);
        FillUInt16Planes(sourcePlane, sourceStride, predictionPlane, predictionStride, width, height, maximumSample);
        FillReference(sourcePlane, sourceStride, predictionPlane, predictionStride, expectedPlane, residualStride, width, height);

        Av1ResidualBuilder.Subtract(sourcePlane, sourceStride, predictionPlane, predictionStride, actualPlane, residualStride, width, height);

        Assert.Equal(expected, actual);
        long expectedSquaredError = CalculateReferenceSquaredError(expectedPlane, residualStride, width, height);

        Assert.Equal(
            expectedSquaredError,
            Av1ResidualBuilder.SumSquaredError(
                sourcePlane,
                sourceStride,
                predictionPlane,
                predictionStride,
                width,
                height));
    }

    private static void FillBytePlanes(Span<byte> source, int sourceStride, Span<byte> prediction, int predictionStride, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                source[(y * sourceStride) + x] = (byte)(((x * 37) + (y * 19) + 251) & byte.MaxValue);
                prediction[(y * predictionStride) + x] = (byte)(((x * 11) + (y * 43) + 127) & byte.MaxValue);
            }
        }

        source[0] = byte.MaxValue;
        prediction[0] = 0;
        source[1] = 0;
        prediction[1] = byte.MaxValue;
    }

    private static void FillUInt16Planes(Span<ushort> source, int sourceStride, Span<ushort> prediction, int predictionStride, int width, int height, int maximumSample)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                source[(y * sourceStride) + x] = (ushort)(((x * 197) + (y * 389) + maximumSample) & maximumSample);
                prediction[(y * predictionStride) + x] = (ushort)(((x * 283) + (y * 151) + (maximumSample / 2)) & maximumSample);
            }
        }

        source[0] = (ushort)maximumSample;
        prediction[0] = 0;
        source[1] = 0;
        prediction[1] = (ushort)maximumSample;
    }

    private static void FillReference(
        ReadOnlySpan<byte> source,
        int sourceStride,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                residual[(y * residualStride) + x] = (short)(source[(y * sourceStride) + x] - prediction[(y * predictionStride) + x]);
            }
        }
    }

    private static void FillReference(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        ReadOnlySpan<ushort> prediction,
        int predictionStride,
        Span<short> residual,
        int residualStride,
        int width,
        int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                residual[(y * residualStride) + x] = (short)(source[(y * sourceStride) + x] - prediction[(y * predictionStride) + x]);
            }
        }
    }

    /// <summary>
    /// Calculates residual energy with scalar arithmetic independently of the vectorized implementation under test.
    /// </summary>
    private static long CalculateReferenceSquaredError(
        ReadOnlySpan<short> residual,
        int residualStride,
        int width,
        int height)
    {
        long squaredError = 0;

        // Only coded samples contribute to the error metric; stride padding must remain outside the calculation.
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int value = residual[(y * residualStride) + x];
                squaredError += value * value;
            }
        }

        return squaredError;
    }

    private static void ValidateCompoundSearchMetrics()
    {
        Random random = new(0x2C0D);
        foreach (int bitDepth in new[] { 8, 10, 12 })
        {
            int maximum = (1 << bitDepth) - 1;
            foreach (int width in new[] { 8, 16, 32, 64, 128 })
            {
                foreach (int height in new[] { 8, 16, width })
                {
                    foreach (bool masked in new[] { false, true })
                    {
                        int sourceStride = width + 5;
                        int predictionStride = width + 11;
                        ushort[] source = new ushort[sourceStride * height];
                        ushort[] prediction = new ushort[predictionStride * height];
                        ushort[] second = new ushort[width * height];
                        byte[] mask = masked ? new byte[width * height] : [];
                        for (int i = 0; i < source.Length; i++)
                        {
                            source[i] = (ushort)random.Next(0, maximum + 1);
                        }

                        for (int i = 0; i < prediction.Length; i++)
                        {
                            prediction[i] = (ushort)random.Next(0, maximum + 1);
                        }

                        for (int i = 0; i < second.Length; i++)
                        {
                            // Every tenth pair is extreme, to reach the largest blend and difference.
                            second[i] = i % 10 == 0 ? (ushort)maximum : (ushort)random.Next(0, maximum + 1);
                        }

                        for (int i = 0; i < mask.Length; i++)
                        {
                            mask[i] = (byte)random.Next(0, 65);
                        }

                        foreach (int rowStep in new[] { 1, 2 })
                        {
                            int expectedSad = 0;
                            for (int y = 0; y < height; y += rowStep)
                            {
                                for (int x = 0; x < width; x++)
                                {
                                    int blended = CompoundBlendReference(prediction[(y * predictionStride) + x], second[(y * width) + x], mask, (y * width) + x);
                                    expectedSad += Math.Abs(source[(y * sourceStride) + x] - blended);
                                }
                            }

                            expectedSad *= rowStep;
                            int actualSad = bitDepth == 8
                                ? Av1ResidualBuilder.SumCompoundAbsoluteDifferences(
                                    ToBytes(source), sourceStride, ToBytes(prediction), predictionStride, ToBytes(second), mask, width, height, rowStep)
                                : Av1ResidualBuilder.SumCompoundAbsoluteDifferences(
                                    source, sourceStride, prediction, predictionStride, second, mask, width, height, rowStep);

                            Assert.Equal(expectedSad, actualSad);
                        }

                        int expectedSum = 0;
                        long expectedSquares = 0;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                int blended = CompoundBlendReference(prediction[(y * predictionStride) + x], second[(y * width) + x], mask, (y * width) + x);
                                int difference = source[(y * sourceStride) + x] - blended;
                                expectedSum += difference;
                                expectedSquares += (long)difference * difference;
                            }
                        }

                        int actualSum;
                        long actualSquares;
                        if (bitDepth == 8)
                        {
                            Av1ResidualBuilder.GetCompoundMoments(
                                ToBytes(source), sourceStride, ToBytes(prediction), predictionStride, ToBytes(second), mask, width, height, out actualSum, out actualSquares);
                        }
                        else
                        {
                            Av1ResidualBuilder.GetCompoundMoments(
                                source, sourceStride, prediction, predictionStride, second, mask, width, height, out actualSum, out actualSquares);
                        }

                        Assert.Equal(expectedSum, actualSum);
                        Assert.Equal(expectedSquares, actualSquares);
                    }
                }
            }
        }
    }

    /// <summary>
    /// The scalar compound blend: comp_avg_pred() for equal weights, AOM_BLEND_A64() for mask weights.
    /// </summary>
    private static int CompoundBlendReference(int searched, int second, byte[] mask, int index)
        => mask.Length == 0
            ? (searched + second + 1) >> 1
            : ((mask[index] * searched) + ((64 - mask[index]) * second) + 32) >> 6;

    private static byte[] ToBytes(ushort[] samples)
    {
        byte[] bytes = new byte[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            bytes[i] = (byte)samples[i];
        }

        return bytes;
    }

    private static void ValidateCorrelation()
    {
        Random random = new(0xC022);
        foreach (int width in new[] { 4, 8, 16, 32, 64 })
        {
            foreach (int height in new[] { 4, 8, 16, 32, 64 })
            {
                foreach (int pattern in new[] { 0, 1, 2 })
                {
                    int stride = width + 3;
                    short[] residual = new short[stride * height];
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            // Random noise, a smooth ramp with noise, and a constant block.
                            residual[(y * stride) + x] = pattern switch
                            {
                                0 => (short)random.Next(-4095, 4096),
                                1 => (short)((x * 37) - (y * 11) + random.Next(-8, 9)),
                                _ => 17,
                            };
                        }
                    }

                    CorrelationReference(residual, stride, width, height, out float expectedHorizontal, out float expectedVertical);
                    Av1ResidualBuilder.GetHorizontalVerticalCorrelation(residual, stride, width, height, out float horizontal, out float vertical);
                    Assert.Equal(expectedHorizontal, horizontal);
                    Assert.Equal(expectedVertical, vertical);
                }
            }
        }
    }

    /// <summary>
    /// The scalar definition of av1_get_horver_correlation_full_c().
    /// </summary>
    private static void CorrelationReference(short[] diff, int stride, int width, int height, out float horizontal, out float vertical)
    {
        long sum = 0;
        long squares = 0;
        long leftProducts = 0;
        long topProducts = 0;
        long firstRow = 0;
        long finalRow = 0;
        long firstColumn = 0;
        long finalColumn = 0;
        long firstRowSquares = 0;
        long finalRowSquares = 0;
        long firstColumnSquares = 0;
        long finalColumnSquares = 0;
        for (int i = 0; i < height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                long x = diff[(i * stride) + j];
                sum += x;
                squares += x * x;
                if (j > 0)
                {
                    leftProducts += x * diff[(i * stride) + j - 1];
                }

                if (i > 0)
                {
                    topProducts += x * diff[((i - 1) * stride) + j];
                }

                if (i == 0)
                {
                    firstRow += x;
                    firstRowSquares += x * x;
                }

                if (i == height - 1)
                {
                    finalRow += x;
                    finalRowSquares += x * x;
                }

                if (j == 0)
                {
                    firstColumn += x;
                    firstColumnSquares += x * x;
                }

                if (j == width - 1)
                {
                    finalColumn += x;
                    finalColumnSquares += x * x;
                }
            }
        }

        long horizontalSum = sum - finalColumn;
        long verticalSum = sum - finalRow;
        long leftSum = sum - firstColumn;
        long topSum = sum - firstRow;
        float horizontalCount = height * (width - 1);
        float verticalCount = (height - 1) * width;
        float horizontalVariance = (squares - finalColumnSquares) - ((horizontalSum * horizontalSum) / horizontalCount);
        float verticalVariance = (squares - finalRowSquares) - ((verticalSum * verticalSum) / verticalCount);
        float leftVariance = (squares - firstColumnSquares) - ((leftSum * leftSum) / horizontalCount);
        float topVariance = (squares - firstRowSquares) - ((topSum * topSum) / verticalCount);
        float leftCovariance = leftProducts - ((horizontalSum * leftSum) / horizontalCount);
        float topCovariance = topProducts - ((verticalSum * topSum) / verticalCount);
        horizontal = horizontalVariance > 0 && leftVariance > 0 ? Math.Max(0, leftCovariance / MathF.Sqrt(horizontalVariance * leftVariance)) : 1;
        vertical = verticalVariance > 0 && topVariance > 0 ? Math.Max(0, topCovariance / MathF.Sqrt(verticalVariance * topVariance)) : 1;
    }
}
