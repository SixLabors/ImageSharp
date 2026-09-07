// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 residual construction against source-minus-prediction reference arithmetic.
/// </summary>
[Trait("Format", "Avif")]
public class Av1ResidualBuilderTests
{
    /// <summary>
    /// Verifies precision limits, independent strides, candidate order, and exact final-row bounds.
    /// </summary>
    [Theory]
    [InlineData(255, false)]
    [InlineData(255, true)]
    public void ByteSearchMetricsMatchKnownMoments(int maximum, bool negative)
    {
        const int SourceStride = 13;
        const int PredictionStride = 17;
        const int SourceOffset = 1;
        const int PredictionOffset = 3;
        byte[] sourceBuffer = new byte[SourceOffset + (7 * SourceStride) + 8];
        byte[] predictionBuffer = new byte[PredictionOffset + (7 * PredictionStride) + 11];
        sourceBuffer.AsSpan().Fill((byte)maximum);
        predictionBuffer.AsSpan().Fill((byte)maximum);
        Span<byte> source = sourceBuffer.AsSpan(SourceOffset);
        Span<byte> prediction = predictionBuffer.AsSpan(PredictionOffset);
        int[] sums = [-1, 0, 0, 0, 0, -1];

        // The last source row contains exactly eight samples, and the four prediction windows require
        // exactly eleven. Distinct nonzero padding catches accidental participation of neighboring rows.
        for (int row = 0; row < 8; row++)
        {
            source.Slice(row * SourceStride, 8).Fill((byte)(negative ? 0 : maximum));
            prediction.Slice(row * PredictionStride, 11).Fill((byte)(negative ? maximum : 0));
        }

        Assert.Equal(64 * maximum, Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride));
        Av1ResidualBuilder.GetMoments8x8(source, SourceStride, prediction, PredictionStride, out int sum, out int squaredSum);
        Assert.Equal((negative ? -64 : 64) * maximum, sum);
        Assert.Equal(64 * maximum * maximum, squaredSum);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride, sums.AsSpan(1, 4));
        Assert.Equal(new[] { -1, 64 * maximum, 64 * maximum, 64 * maximum, 64 * maximum, -1 }, sums);

        // Source (2*y + x) and prediction (y + 2*x) give residual (y - x) * step, exercising both signs
        // and distinct rows. Across the 8x8 square its signed sum is zero, absolute sum 168, and squared sum 672.
        // Offsetting prediction by 1, 2, or 3 columns gives absolute sums 198, 276, and 386 respectively.
        int step = maximum / 32;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                source[(row * SourceStride) + column] = (byte)(((2 * row) + column) * step);
            }

            for (int column = 0; column < 11; column++)
            {
                prediction[(row * PredictionStride) + column] = (byte)((row + (2 * column)) * step);
            }
        }

        Assert.Equal(168 * step, Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride));
        Av1ResidualBuilder.GetMoments8x8(source, SourceStride, prediction, PredictionStride, out sum, out squaredSum);
        Assert.Equal(0, sum);
        Assert.Equal(672 * step * step, squaredSum);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride, sums.AsSpan(1, 4));
        Assert.Equal(new[] { -1, 168 * step, 198 * step, 276 * step, 386 * step, -1 }, sums);
    }

    /// <summary>
    /// Verifies precision limits, independent strides, candidate order, and exact final-row bounds.
    /// </summary>
    [Theory]
    [InlineData(1023, false)]
    [InlineData(1023, true)]
    [InlineData(4095, false)]
    [InlineData(4095, true)]
    public void HighBitDepthSearchMetricsMatchKnownMoments(int maximum, bool negative)
    {
        const int SourceStride = 13;
        const int PredictionStride = 17;
        const int SourceOffset = 1;
        const int PredictionOffset = 3;
        ushort[] sourceBuffer = new ushort[SourceOffset + (7 * SourceStride) + 8];
        ushort[] predictionBuffer = new ushort[PredictionOffset + (7 * PredictionStride) + 11];
        sourceBuffer.AsSpan().Fill((ushort)maximum);
        predictionBuffer.AsSpan().Fill((ushort)maximum);
        Span<ushort> source = sourceBuffer.AsSpan(SourceOffset);
        Span<ushort> prediction = predictionBuffer.AsSpan(PredictionOffset);
        int[] sums = [-1, 0, 0, 0, 0, -1];

        // The last source row contains exactly eight samples, and the four prediction windows require
        // exactly eleven. Distinct nonzero padding catches accidental participation of neighboring rows.
        for (int row = 0; row < 8; row++)
        {
            source.Slice(row * SourceStride, 8).Fill((ushort)(negative ? 0 : maximum));
            prediction.Slice(row * PredictionStride, 11).Fill((ushort)(negative ? maximum : 0));
        }

        Assert.Equal(64 * maximum, Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride));
        Av1ResidualBuilder.GetMoments8x8(source, SourceStride, prediction, PredictionStride, out int sum, out int squaredSum);
        Assert.Equal((negative ? -64 : 64) * maximum, sum);
        Assert.Equal(64 * maximum * maximum, squaredSum);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride, sums.AsSpan(1, 4));
        Assert.Equal(new[] { -1, 64 * maximum, 64 * maximum, 64 * maximum, 64 * maximum, -1 }, sums);

        // Source (2*y + x) and prediction (y + 2*x) give residual (y - x) * step, exercising both signs
        // and distinct rows. Across the 8x8 square its signed sum is zero, absolute sum 168, and squared sum 672.
        // Offsetting prediction by 1, 2, or 3 columns gives absolute sums 198, 276, and 386 respectively.
        int step = maximum / 32;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                source[(row * SourceStride) + column] = (ushort)(((2 * row) + column) * step);
            }

            for (int column = 0; column < 11; column++)
            {
                prediction[(row * PredictionStride) + column] = (ushort)((row + (2 * column)) * step);
            }
        }

        Assert.Equal(168 * step, Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride));
        Av1ResidualBuilder.GetMoments8x8(source, SourceStride, prediction, PredictionStride, out sum, out squaredSum);
        Assert.Equal(0, sum);
        Assert.Equal(672 * step * step, squaredSum);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, SourceStride, prediction, PredictionStride, sums.AsSpan(1, 4));
        Assert.Equal(new[] { -1, 168 * step, 198 * step, 276 * step, 386 * step, -1 }, sums);
    }

    private const HwIntrinsics ResidualConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Checks rectangular search costs, alternate-row sampling, and wide moments across hardware paths.
    /// </summary>
    [Fact]
    public void RectangularSearchMetricsMatchScalarAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateRectangularSearchMetrics, ResidualConfigurations);

    /// <summary>
    /// Verifies 8-bit, 10-bit, and 12-bit residuals across misaligned planes, independent strides, and SIMD tails.
    /// </summary>
    [Fact]
    public void ResidualsMatchReferenceAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateResiduals, ResidualConfigurations);

    /// <summary>
    /// Verifies every width-specific operator even when the current processor cannot select that width in the driver.
    /// </summary>
    [Fact]
    public void ResidualOperatorsMatchReferenceAtEveryVectorWidth()
    {
        byte[] byteSource = new byte[Vector512<byte>.Count];
        byte[] bytePrediction = new byte[Vector512<byte>.Count];
        short[] byteExpected = new short[Vector512<byte>.Count];
        short[] byteActual = new short[Vector512<byte>.Count];
        FillBytePlanes(byteSource, byteSource.Length, bytePrediction, bytePrediction.Length, byteSource.Length, 1);
        FillReference(byteSource, byteSource.Length, bytePrediction, bytePrediction.Length, byteExpected, byteExpected.Length, byteExpected.Length, 1);

        ref byte byteSourceBase = ref MemoryMarshal.GetArrayDataReference(byteSource);
        ref byte bytePredictionBase = ref MemoryMarshal.GetArrayDataReference(bytePrediction);
        Vector128<short> byteLower128 = Av1ResidualBuilder.ByteOperator.Subtract(
            Unsafe.As<byte, Vector128<byte>>(ref byteSourceBase),
            Unsafe.As<byte, Vector128<byte>>(ref bytePredictionBase),
            out Vector128<short> byteUpper128);

        byteLower128.CopyTo(byteActual);
        byteUpper128.CopyTo(byteActual.AsSpan(Vector128<short>.Count));
        AssertEqual(byteExpected, byteActual, Vector128<byte>.Count);

        Vector256<short> byteLower256 = Av1ResidualBuilder.ByteOperator.Subtract(
            Unsafe.As<byte, Vector256<byte>>(ref byteSourceBase),
            Unsafe.As<byte, Vector256<byte>>(ref bytePredictionBase),
            out Vector256<short> byteUpper256);

        byteLower256.CopyTo(byteActual);
        byteUpper256.CopyTo(byteActual.AsSpan(Vector256<short>.Count));
        AssertEqual(byteExpected, byteActual, Vector256<byte>.Count);

        Vector512<short> byteLower512 = Av1ResidualBuilder.ByteOperator.Subtract(
            Unsafe.As<byte, Vector512<byte>>(ref byteSourceBase),
            Unsafe.As<byte, Vector512<byte>>(ref bytePredictionBase),
            out Vector512<short> byteUpper512);

        byteLower512.CopyTo(byteActual);
        byteUpper512.CopyTo(byteActual.AsSpan(Vector512<short>.Count));
        AssertEqual(byteExpected, byteActual, Vector512<byte>.Count);

        ushort[] uint16Source = new ushort[Vector512<ushort>.Count];
        ushort[] uint16Prediction = new ushort[Vector512<ushort>.Count];
        short[] uint16Expected = new short[Vector512<ushort>.Count];
        short[] uint16Actual = new short[Vector512<ushort>.Count];
        FillUInt16Planes(uint16Source, uint16Source.Length, uint16Prediction, uint16Prediction.Length, uint16Source.Length, 1, 4095);
        FillReference(uint16Source, uint16Source.Length, uint16Prediction, uint16Prediction.Length, uint16Expected, uint16Expected.Length, uint16Expected.Length, 1);

        ref ushort uint16SourceBase = ref MemoryMarshal.GetArrayDataReference(uint16Source);
        ref ushort uint16PredictionBase = ref MemoryMarshal.GetArrayDataReference(uint16Prediction);
        Av1ResidualBuilder.UInt16Operator.Subtract(
            Unsafe.As<ushort, Vector128<ushort>>(ref uint16SourceBase),
            Unsafe.As<ushort, Vector128<ushort>>(ref uint16PredictionBase),
            out _).CopyTo(uint16Actual);

        AssertEqual(uint16Expected, uint16Actual, Vector128<ushort>.Count);

        Av1ResidualBuilder.UInt16Operator.Subtract(
            Unsafe.As<ushort, Vector256<ushort>>(ref uint16SourceBase),
            Unsafe.As<ushort, Vector256<ushort>>(ref uint16PredictionBase),
            out _).CopyTo(uint16Actual);

        AssertEqual(uint16Expected, uint16Actual, Vector256<ushort>.Count);

        Av1ResidualBuilder.UInt16Operator.Subtract(
            Unsafe.As<ushort, Vector512<ushort>>(ref uint16SourceBase),
            Unsafe.As<ushort, Vector512<ushort>>(ref uint16PredictionBase),
            out _).CopyTo(uint16Actual);

        AssertEqual(uint16Expected, uint16Actual, Vector512<ushort>.Count);
    }

    /// <summary>
    /// Verifies exact 12-bit residual energy through every hardware-selected vector width and the scalar tail.
    /// </summary>
    [Fact]
    public void SumSquaresMatchesScalarAcrossHardwareWidths()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateSumSquares, ResidualConfigurations);

    /// <summary>
    /// Verifies that repeated maximum-transform residual and squared-error processing uses only caller-owned buffers.
    /// </summary>
    [Fact]
    public void ResidualConstructionDoesNotAllocate()
    {
        const int width = 64;
        const int height = 64;
        byte[] source = new byte[width * height];
        byte[] prediction = new byte[width * height];
        short[] residual = new short[width * height];
        ushort[] highBitDepthSource = new ushort[width * height];
        ushort[] highBitDepthPrediction = new ushort[width * height];
        short[] highBitDepthResidual = new short[width * height];
        FillBytePlanes(source, width, prediction, width, width, height);
        FillUInt16Planes(highBitDepthSource, width, highBitDepthPrediction, width, width, height, 4095);

        Av1ResidualBuilder.Subtract(source, width, prediction, width, residual, width, width, height);
        Av1ResidualBuilder.Subtract(highBitDepthSource, width, highBitDepthPrediction, width, highBitDepthResidual, width, width, height);
        _ = Av1ResidualBuilder.SumSquaredError(source, width, prediction, width, width, height);
        _ = Av1ResidualBuilder.SumSquaredError(
            highBitDepthSource,
            width,
            highBitDepthPrediction,
            width,
            width,
            height);

        int[] candidateSums = new int[4];
        _ = Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, width, prediction, width);
        _ = Av1ResidualBuilder.SumAbsoluteDifferences8x8(highBitDepthSource, width, highBitDepthPrediction, width);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, width, prediction, width, candidateSums);
        Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(highBitDepthSource, width, highBitDepthPrediction, width, candidateSums);
        Av1ResidualBuilder.GetMoments8x8(source, width, prediction, width, out _, out _);
        Av1ResidualBuilder.GetMoments8x8(highBitDepthSource, width, highBitDepthPrediction, width, out _, out _);

        long sum = 0;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1ResidualBuilder.Subtract(source, width, prediction, width, residual, width, width, height);
            Av1ResidualBuilder.Subtract(highBitDepthSource, width, highBitDepthPrediction, width, highBitDepthResidual, width, width, height);
            sum += Av1ResidualBuilder.SumSquaredError(source, width, prediction, width, width, height);
            sum += Av1ResidualBuilder.SumSquaredError(
                highBitDepthSource,
                width,
                highBitDepthPrediction,
                width,
                width,
                height);

            sum += Av1ResidualBuilder.SumAbsoluteDifferences8x8(source, width, prediction, width);
            sum += Av1ResidualBuilder.SumAbsoluteDifferences8x8(highBitDepthSource, width, highBitDepthPrediction, width);
            Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(source, width, prediction, width, candidateSums);
            sum += candidateSums[0];
            Av1ResidualBuilder.SumFourAbsoluteDifferences8x8(highBitDepthSource, width, highBitDepthPrediction, width, candidateSums);
            sum += candidateSums[3];
            Av1ResidualBuilder.GetMoments8x8(source, width, prediction, width, out int byteSum, out int byteSquares);
            Av1ResidualBuilder.GetMoments8x8(highBitDepthSource, width, highBitDepthPrediction, width, out int wordSum, out int wordSquares);
            sum += byteSum + byteSquares + wordSum + wordSquares;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(sum > 0);
        Assert.Equal(0, allocated);
    }

    /// <summary>
    /// Exercises independent strides and exact final-row lengths, including every coded block dimension and vector tails.
    /// </summary>
    private static void ValidateRectangularSearchMetrics()
    {
        foreach (int width in new[] { 4, 8, 12, 16, 24, 31, 32, 63, 64, 127, 128 })
        {
            foreach (int height in new[] { 4, 8, 16, 32, 64, 128 })
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

    private static void AssertEqual(ReadOnlySpan<short> expected, ReadOnlySpan<short> actual, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }
}
