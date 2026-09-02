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
    private const HwIntrinsics ResidualConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

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
    /// Verifies that repeated maximum-transform residual construction uses only caller-owned buffers.
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

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 32; iteration++)
        {
            Av1ResidualBuilder.Subtract(source, width, prediction, width, residual, width, width, height);
            Av1ResidualBuilder.Subtract(highBitDepthSource, width, highBitDepthPrediction, width, highBitDepthResidual, width, width, height);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
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

    private static void AssertEqual(ReadOnlySpan<short> expected, ReadOnlySpan<short> actual, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }
}
