// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Reconstructs dequantized HEVC transform coefficients.
/// </summary>
internal static class HevcInverseQuantizer
{
    /// <summary>
    /// Gets the inverse quantization scale selected by the quantization-parameter remainder.
    /// </summary>
    private static ReadOnlySpan<byte> InverseQuantizationScales => [40, 45, 51, 57, 64, 72];

    /// <summary>
    /// Dequantizes one square transform block using the effective component quantization parameter.
    /// </summary>
    /// <param name="quantized">The decoded quantized coefficients in raster order.</param>
    /// <param name="destination">The destination dequantized coefficients in raster order.</param>
    /// <param name="log2Size">The base-two logarithm of the transform-block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="quantizationParameter">The effective nonnegative component quantization parameter including its bit-depth offset.</param>
    /// <param name="scalingListEnabled">Whether the governing sequence enables scaling lists.</param>
    /// <param name="scalingList">The effective picture scaling matrices.</param>
    /// <param name="plane">The reconstructed color plane.</param>
    /// <param name="isIntraPredicted">Whether the transform block belongs to an intra-predicted coding unit.</param>
    /// <param name="transformSkip">Whether the transform block bypasses the inverse transform.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether transform-skip precision is extended by the sequence.</param>
    public static void Dequantize(
        ReadOnlySpan<int> quantized,
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
        DebugGuard.MustBeBetweenOrEqualTo(log2Size, 2, 5, nameof(log2Size));
        DebugGuard.MustBeGreaterThanOrEqualTo(quantizationParameter, 0, nameof(quantizationParameter));
        int size = 1 << log2Size;
        int sampleCount = size * size;
        DebugGuard.IsTrue(quantized.Length >= sampleCount, "The quantized coefficient span is shorter than the transform block.");
        DebugGuard.IsTrue(destination.Length >= sampleCount, "The dequantized coefficient span is shorter than the transform block.");

        int transformShift = maxTransformDynamicRange - bitDepth - log2Size;
        if (transformSkip && extendedPrecisionProcessingEnabled)
        {
            transformShift = Math.Max(0, transformShift);
        }

        int quantizationParameterPer = quantizationParameter / 6;
        int quantizationParameterRemainder = quantizationParameter % 6;
        int inverseQuantizationScale = InverseQuantizationScales[quantizationParameterRemainder];
        bool useScalingList = scalingListEnabled && (!transformSkip || log2Size == 2);
        int rightShift = 6 - (transformShift + quantizationParameterPer) + (useScalingList ? 4 : 0);
        int outputMinimum = -(1 << maxTransformDynamicRange);
        int outputMaximum = (1 << maxTransformDynamicRange) - 1;

        // The input clip is part of the normative dequantization process. Its right-shift dependency ensures the
        // following signed 32-bit multiplication and optional left shift cannot overflow for any valid coefficient.
        int scaleBits = useScalingList ? 15 : 7;
        int targetInputBitDepth = Math.Min(maxTransformDynamicRange + 1, 32 + rightShift - scaleBits);
        int inputMinimum = -(1 << (targetInputBitDepth - 1));
        int inputMaximum = (1 << (targetInputBitDepth - 1)) - 1;

        if (!useScalingList)
        {
            DequantizeUniform(
                quantized[..sampleCount],
                destination[..sampleCount],
                inverseQuantizationScale,
                rightShift,
                inputMinimum,
                inputMaximum,
                outputMinimum,
                outputMaximum);

            return;
        }

        int sizeId = log2Size - 2;
        int matrixId = (isIntraPredicted ? 0 : 3) + (int)plane;
        ReadOnlySpan<byte> matrix = scalingList.GetExpandedMatrix(sizeId, matrixId);
        DequantizeScalingList(
            quantized[..sampleCount],
            destination[..sampleCount],
            matrix,
            inverseQuantizationScale,
            rightShift,
            inputMinimum,
            inputMaximum,
            outputMinimum,
            outputMaximum);
    }

    /// <summary>
    /// Dequantizes coefficients using one uniform inverse-quantization scale.
    /// </summary>
    /// <param name="source">The complete quantized coefficient block.</param>
    /// <param name="destination">The complete dequantized coefficient block.</param>
    /// <param name="scale">The inverse-quantization scale.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    private static void DequantizeUniform(
        ReadOnlySpan<int> source,
        Span<int> destination,
        int scale,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(source);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector512<int>.Count;
            Vector512<int> weights = Vector512.Create(scale);
            for (; i <= oneVectorFromEnd; i += Vector512<int>.Count)
            {
                Vector512<int> values = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector256<int>.Count;
            Vector256<int> weights = Vector256.Create(scale);
            for (; i <= oneVectorFromEnd; i += Vector256<int>.Count)
            {
                Vector256<int> values = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector128<int>.Count;
            Vector128<int> weights = Vector128.Create(scale);
            for (; i <= oneVectorFromEnd; i += Vector128<int>.Count)
            {
                Vector128<int> values = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            destination[i] = Dequantize(source[i], scale, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum);
        }
    }

    /// <summary>
    /// Dequantizes coefficients using the expanded scaling matrix selected for the transform block.
    /// </summary>
    /// <param name="source">The complete quantized coefficient block.</param>
    /// <param name="destination">The complete dequantized coefficient block.</param>
    /// <param name="matrix">The scaling matrix expanded to the transform dimensions.</param>
    /// <param name="inverseQuantizationScale">The inverse-quantization scale.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    private static void DequantizeScalingList(
        ReadOnlySpan<int> source,
        Span<int> destination,
        ReadOnlySpan<byte> matrix,
        int inverseQuantizationScale,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(source);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        ref byte matrixBase = ref MemoryMarshal.GetReference(matrix);
        int i = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector512<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<int>.Count)
            {
                Vector512<int> values = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector512<int> weights = LoadScalingWeightsVector512(ref Unsafe.Add(ref matrixBase, i), inverseQuantizationScale);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector256<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<int>.Count)
            {
                Vector256<int> values = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector256<int> weights = LoadScalingWeightsVector256(ref Unsafe.Add(ref matrixBase, i), inverseQuantizationScale);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = source.Length - Vector128<int>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<int>.Count)
            {
                Vector128<int> values = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector128<int> weights = LoadScalingWeightsVector128(ref Unsafe.Add(ref matrixBase, i), inverseQuantizationScale);
                Dequantize(values, weights, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum).StoreUnsafe(ref destinationBase, (nuint)i);
            }
        }

        for (; i < source.Length; i++)
        {
            int weight = Unsafe.Add(ref matrixBase, i) * inverseQuantizationScale;
            destination[i] = Dequantize(source[i], weight, rightShift, inputMinimum, inputMaximum, outputMinimum, outputMaximum);
        }
    }

    /// <summary>
    /// Loads and widens sixteen scaling coefficients for a 512-bit coefficient vector.
    /// </summary>
    /// <param name="source">The first scaling coefficient.</param>
    /// <param name="scale">The inverse-quantization scale.</param>
    /// <returns>The sixteen ordered dequantization weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> LoadScalingWeightsVector512(ref byte source, int scale)
    {
        Vector128<byte> packed = Vector128.LoadUnsafe(ref source);
        (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(packed);
        Vector256<uint> lower32 = Vector256.Create(Vector128.WidenLower(lower16), Vector128.WidenUpper(lower16));
        Vector256<uint> upper32 = Vector256.Create(Vector128.WidenLower(upper16), Vector128.WidenUpper(upper16));
        return Vector512.Create(lower32, upper32).AsInt32() * Vector512.Create(scale);
    }

    /// <summary>
    /// Loads and widens eight scaling coefficients for a 256-bit coefficient vector.
    /// </summary>
    /// <param name="source">The first scaling coefficient.</param>
    /// <param name="scale">The inverse-quantization scale.</param>
    /// <returns>The eight ordered dequantization weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> LoadScalingWeightsVector256(ref byte source, int scale)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref source);
        Vector128<ushort> values16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        Vector256<uint> values32 = Vector256.Create(Vector128.WidenLower(values16), Vector128.WidenUpper(values16));
        return values32.AsInt32() * Vector256.Create(scale);
    }

    /// <summary>
    /// Loads and widens four scaling coefficients for a 128-bit coefficient vector.
    /// </summary>
    /// <param name="source">The first scaling coefficient.</param>
    /// <param name="scale">The inverse-quantization scale.</param>
    /// <returns>The four ordered dequantization weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> LoadScalingWeightsVector128(ref byte source, int scale)
    {
        uint packed = Unsafe.ReadUnaligned<uint>(ref source);
        Vector128<ushort> values16 = Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsByte());
        Vector128<uint> values32 = Vector128.WidenLower(values16);
        return values32.AsInt32() * Vector128.Create(scale);
    }

    /// <summary>
    /// Dequantizes sixteen signed coefficients with independent scaling weights.
    /// </summary>
    /// <param name="values">The quantized coefficients.</param>
    /// <param name="weights">The dequantization weights.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    /// <returns>The dequantized coefficients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> Dequantize(
        Vector512<int> values,
        Vector512<int> weights,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        values = Vector512.Clamp(values, Vector512.Create(inputMinimum), Vector512.Create(inputMaximum));
        Vector512<int> result = values * weights;
        result = rightShift > 0
            ? (result + Vector512.Create(1 << (rightShift - 1))) >> rightShift
            : result << -rightShift;

        return Vector512.Clamp(result, Vector512.Create(outputMinimum), Vector512.Create(outputMaximum));
    }

    /// <summary>
    /// Dequantizes eight signed coefficients with independent scaling weights.
    /// </summary>
    /// <param name="values">The quantized coefficients.</param>
    /// <param name="weights">The dequantization weights.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    /// <returns>The dequantized coefficients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Dequantize(
        Vector256<int> values,
        Vector256<int> weights,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        values = Vector256.Clamp(values, Vector256.Create(inputMinimum), Vector256.Create(inputMaximum));
        Vector256<int> result = values * weights;
        result = rightShift > 0
            ? (result + Vector256.Create(1 << (rightShift - 1))) >> rightShift
            : result << -rightShift;

        return Vector256.Clamp(result, Vector256.Create(outputMinimum), Vector256.Create(outputMaximum));
    }

    /// <summary>
    /// Dequantizes four signed coefficients with independent scaling weights.
    /// </summary>
    /// <param name="values">The quantized coefficients.</param>
    /// <param name="weights">The dequantization weights.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    /// <returns>The dequantized coefficients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Dequantize(
        Vector128<int> values,
        Vector128<int> weights,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        values = Vector128.Clamp(values, Vector128.Create(inputMinimum), Vector128.Create(inputMaximum));
        Vector128<int> result = values * weights;
        result = rightShift > 0
            ? (result + Vector128.Create(1 << (rightShift - 1))) >> rightShift
            : result << -rightShift;

        return Vector128.Clamp(result, Vector128.Create(outputMinimum), Vector128.Create(outputMaximum));
    }

    /// <summary>
    /// Dequantizes one signed coefficient.
    /// </summary>
    /// <param name="value">The quantized coefficient.</param>
    /// <param name="weight">The dequantization weight.</param>
    /// <param name="rightShift">The signed normalization shift.</param>
    /// <param name="inputMinimum">The inclusive quantized input minimum.</param>
    /// <param name="inputMaximum">The inclusive quantized input maximum.</param>
    /// <param name="outputMinimum">The inclusive dequantized output minimum.</param>
    /// <param name="outputMaximum">The inclusive dequantized output maximum.</param>
    /// <returns>The dequantized coefficient.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Dequantize(
        int value,
        int weight,
        int rightShift,
        int inputMinimum,
        int inputMaximum,
        int outputMinimum,
        int outputMaximum)
    {
        int result = Math.Clamp(value, inputMinimum, inputMaximum) * weight;
        result = rightShift > 0 ? (result + (1 << (rightShift - 1))) >> rightShift : result << -rightShift;
        return Math.Clamp(result, outputMinimum, outputMaximum);
    }
}
