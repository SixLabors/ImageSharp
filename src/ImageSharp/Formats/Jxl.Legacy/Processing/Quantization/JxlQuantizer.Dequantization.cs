// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;

internal sealed partial class JxlQuantizer
{
    /// <summary>
    /// Reconstructs one quantized channel in the encoder's coefficient workspace.
    /// </summary>
    /// <param name="coefficients">The logical integer coefficients.</param>
    /// <param name="matrix">The corresponding dequantization weights.</param>
    /// <param name="inverseQuantization">The block's inverse AC quantization scale.</param>
    /// <param name="channelIndex">The XYB channel index.</param>
    /// <param name="biases">The three channel biases and shared reciprocal numerator.</param>
    /// <param name="destination">The caller-owned float coefficient workspace.</param>
    public static void Dequantize(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<float> matrix,
        float inverseQuantization,
        int channelIndex,
        ReadOnlySpan<float> biases,
        Span<float> destination)
    {
        QuantizationBiasOperator bias = new(channelIndex, biases);
        ref int input = ref MemoryMarshal.GetReference(coefficients);
        ref float weights = ref MemoryMarshal.GetReference(matrix);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int i = 0;

        // The encoder first applies the matrix, then the inverse quantization scale.
        // Keep both products separate and in this order, as required by enc_group.cc.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector512Count<float>();
            Vector512<float> scale = Vector512.Create(inverseQuantization);

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> quantized = Vector512.ConvertToSingle(Vector512.LoadUnsafe(ref input, (uint)i));
                Vector512<float> weight = Vector512.LoadUnsafe(ref weights, (uint)i);
                ((bias.Invoke(quantized) * weight) * scale).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector256Count<float>();
            Vector256<float> scale = Vector256.Create(inverseQuantization);

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> quantized = Vector256.ConvertToSingle(Vector256.LoadUnsafe(ref input, (uint)i));
                Vector256<float> weight = Vector256.LoadUnsafe(ref weights, (uint)i);
                ((bias.Invoke(quantized) * weight) * scale).StoreUnsafe(ref output, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (coefficients.Length - i).Vector128Count<float>();
            Vector128<float> scale = Vector128.Create(inverseQuantization);

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> quantized = Vector128.ConvertToSingle(Vector128.LoadUnsafe(ref input, (uint)i));
                Vector128<float> weight = Vector128.LoadUnsafe(ref weights, (uint)i);
                ((bias.Invoke(quantized) * weight) * scale).StoreUnsafe(ref output, (uint)i);
            }
        }

        // Complete vectors never read padding. The tail uses the same numeric conversion and product order.
        for (; i < coefficients.Length; i++)
        {
            Unsafe.Add(ref output, i) = (bias.Invoke(Unsafe.Add(ref input, i)) * Unsafe.Add(ref weights, i)) * inverseQuantization;
        }
    }

    /// <summary>
    /// Restores three 16-bit AC coefficient planes and their X/B correlation with Y.
    /// </summary>
    /// <param name="x">The logical X coefficients.</param>
    /// <param name="y">The logical Y coefficients, with the same length as X.</param>
    /// <param name="b">The logical B coefficients, with the same length as X.</param>
    /// <param name="matrices">Three consecutive dequantization matrices in X, Y, B order.</param>
    /// <param name="xScale">The inverse quantization scale including the X matrix multiplier.</param>
    /// <param name="yScale">The inverse quantization scale.</param>
    /// <param name="bScale">The inverse quantization scale including the B matrix multiplier.</param>
    /// <param name="xCorrelation">The Y contribution to X.</param>
    /// <param name="bCorrelation">The Y contribution to B.</param>
    /// <param name="biases">The three channel biases and shared reciprocal numerator.</param>
    /// <param name="destination">Three consecutive caller-owned output planes in X, Y, B order.</param>
    public static void Dequantize(
        ReadOnlySpan<short> x,
        ReadOnlySpan<short> y,
        ReadOnlySpan<short> b,
        ReadOnlySpan<float> matrices,
        float xScale,
        float yScale,
        float bScale,
        float xCorrelation,
        float bCorrelation,
        ReadOnlySpan<float> biases,
        Span<float> destination)
        => DequantizeCore(x, y, b, matrices, xScale, yScale, bScale, xCorrelation, bCorrelation, biases, destination);

    /// <summary>
    /// Restores three 32-bit AC coefficient planes and their X/B correlation with Y.
    /// </summary>
    /// <param name="x">The logical X coefficients.</param>
    /// <param name="y">The logical Y coefficients, with the same length as X.</param>
    /// <param name="b">The logical B coefficients, with the same length as X.</param>
    /// <param name="matrices">Three consecutive dequantization matrices in X, Y, B order.</param>
    /// <param name="xScale">The inverse quantization scale including the X matrix multiplier.</param>
    /// <param name="yScale">The inverse quantization scale.</param>
    /// <param name="bScale">The inverse quantization scale including the B matrix multiplier.</param>
    /// <param name="xCorrelation">The Y contribution to X.</param>
    /// <param name="bCorrelation">The Y contribution to B.</param>
    /// <param name="biases">The three channel biases and shared reciprocal numerator.</param>
    /// <param name="destination">Three consecutive caller-owned output planes in X, Y, B order.</param>
    public static void Dequantize(
        ReadOnlySpan<int> x,
        ReadOnlySpan<int> y,
        ReadOnlySpan<int> b,
        ReadOnlySpan<float> matrices,
        float xScale,
        float yScale,
        float bScale,
        float xCorrelation,
        float bCorrelation,
        ReadOnlySpan<float> biases,
        Span<float> destination)
        => DequantizeCore(x, y, b, matrices, xScale, yScale, bScale, xCorrelation, bCorrelation, biases, destination);

    /// <summary>
    /// Traverses three coefficient planes without staging converted samples or making a separate correlation pass.
    /// </summary>
    /// <typeparam name="TCoefficient">Either short or int, selected by the two storage-specific entry points.</typeparam>
    /// <param name="x">The logical X coefficients.</param>
    /// <param name="y">The logical Y coefficients.</param>
    /// <param name="b">The logical B coefficients.</param>
    /// <param name="matrices">The consecutive X, Y and B matrices.</param>
    /// <param name="xScale">The X matrix scale.</param>
    /// <param name="yScale">The Y matrix scale.</param>
    /// <param name="bScale">The B matrix scale.</param>
    /// <param name="xCorrelation">The Y contribution to X.</param>
    /// <param name="bCorrelation">The Y contribution to B.</param>
    /// <param name="biases">The channel biases and shared reciprocal numerator.</param>
    /// <param name="destination">The consecutive X, Y and B output planes.</param>
    private static void DequantizeCore<TCoefficient>(
        ReadOnlySpan<TCoefficient> x,
        ReadOnlySpan<TCoefficient> y,
        ReadOnlySpan<TCoefficient> b,
        ReadOnlySpan<float> matrices,
        float xScale,
        float yScale,
        float bScale,
        float xCorrelation,
        float bCorrelation,
        ReadOnlySpan<float> biases,
        Span<float> destination)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        int length = x.Length;
        QuantizationBiasOperator xBias = new(0, biases);
        QuantizationBiasOperator yBias = new(1, biases);
        QuantizationBiasOperator bBias = new(2, biases);
        ref TCoefficient inputX = ref MemoryMarshal.GetReference(x);
        ref TCoefficient inputY = ref MemoryMarshal.GetReference(y);
        ref TCoefficient inputB = ref MemoryMarshal.GetReference(b);
        ref float matrixX = ref MemoryMarshal.GetReference(matrices);
        ref float matrixY = ref Unsafe.Add(ref matrixX, length);
        ref float matrixB = ref Unsafe.Add(ref matrixY, length);
        ref float outputX = ref MemoryMarshal.GetReference(destination);
        ref float outputY = ref Unsafe.Add(ref outputX, length);
        ref float outputB = ref Unsafe.Add(ref outputY, length);
        int i = 0;

        // Each lane holds the same frequency in all three channels. Scale each matrix before applying its bias-adjusted coefficient.
        // Correlation uses the restored Y value at that frequency. It does not mix neighboring coefficients or pixel samples.
        if (Vector512.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector512Count<float>();
            Vector512<float> scaleX = Vector512.Create(xScale);
            Vector512<float> scaleY = Vector512.Create(yScale);
            Vector512<float> scaleB = Vector512.Create(bScale);
            Vector512<float> correlateX = Vector512.Create(xCorrelation);
            Vector512<float> correlateB = Vector512.Create(bCorrelation);

            for (nuint block = 0; block < count; block++, i += Vector512<float>.Count)
            {
                Vector512<float> restoredX = xBias.Invoke(Load512(ref inputX, (uint)i)) * (Vector512.LoadUnsafe(ref matrixX, (uint)i) * scaleX);
                Vector512<float> restoredY = yBias.Invoke(Load512(ref inputY, (uint)i)) * (Vector512.LoadUnsafe(ref matrixY, (uint)i) * scaleY);
                Vector512<float> restoredB = bBias.Invoke(Load512(ref inputB, (uint)i)) * (Vector512.LoadUnsafe(ref matrixB, (uint)i) * scaleB);
                Vector512.MultiplyAddEstimate(correlateX, restoredY, restoredX).StoreUnsafe(ref outputX, (uint)i);
                restoredY.StoreUnsafe(ref outputY, (uint)i);
                Vector512.MultiplyAddEstimate(correlateB, restoredY, restoredB).StoreUnsafe(ref outputB, (uint)i);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector256Count<float>();
            Vector256<float> scaleX = Vector256.Create(xScale);
            Vector256<float> scaleY = Vector256.Create(yScale);
            Vector256<float> scaleB = Vector256.Create(bScale);
            Vector256<float> correlateX = Vector256.Create(xCorrelation);
            Vector256<float> correlateB = Vector256.Create(bCorrelation);

            for (nuint block = 0; block < count; block++, i += Vector256<float>.Count)
            {
                Vector256<float> restoredX = xBias.Invoke(Load256(ref inputX, (uint)i)) * (Vector256.LoadUnsafe(ref matrixX, (uint)i) * scaleX);
                Vector256<float> restoredY = yBias.Invoke(Load256(ref inputY, (uint)i)) * (Vector256.LoadUnsafe(ref matrixY, (uint)i) * scaleY);
                Vector256<float> restoredB = bBias.Invoke(Load256(ref inputB, (uint)i)) * (Vector256.LoadUnsafe(ref matrixB, (uint)i) * scaleB);
                Vector256.MultiplyAddEstimate(correlateX, restoredY, restoredX).StoreUnsafe(ref outputX, (uint)i);
                restoredY.StoreUnsafe(ref outputY, (uint)i);
                Vector256.MultiplyAddEstimate(correlateB, restoredY, restoredB).StoreUnsafe(ref outputB, (uint)i);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            nuint count = (length - i).Vector128Count<float>();
            Vector128<float> scaleX = Vector128.Create(xScale);
            Vector128<float> scaleY = Vector128.Create(yScale);
            Vector128<float> scaleB = Vector128.Create(bScale);
            Vector128<float> correlateX = Vector128.Create(xCorrelation);
            Vector128<float> correlateB = Vector128.Create(bCorrelation);

            for (nuint block = 0; block < count; block++, i += Vector128<float>.Count)
            {
                Vector128<float> restoredX = xBias.Invoke(Load128(ref inputX, (uint)i)) * (Vector128.LoadUnsafe(ref matrixX, (uint)i) * scaleX);
                Vector128<float> restoredY = yBias.Invoke(Load128(ref inputY, (uint)i)) * (Vector128.LoadUnsafe(ref matrixY, (uint)i) * scaleY);
                Vector128<float> restoredB = bBias.Invoke(Load128(ref inputB, (uint)i)) * (Vector128.LoadUnsafe(ref matrixB, (uint)i) * scaleB);
                Vector128.MultiplyAddEstimate(correlateX, restoredY, restoredX).StoreUnsafe(ref outputX, (uint)i);
                restoredY.StoreUnsafe(ref outputY, (uint)i);
                Vector128.MultiplyAddEstimate(correlateB, restoredY, restoredB).StoreUnsafe(ref outputB, (uint)i);
            }
        }

        // Smaller widths continue from the first unprocessed frequency. Scalar conversion also sign-extends short coefficients.
        for (; i < length; i++)
        {
            float restoredX = xBias.Invoke(float.CreateTruncating(Unsafe.Add(ref inputX, i))) * (Unsafe.Add(ref matrixX, i) * xScale);
            float restoredY = yBias.Invoke(float.CreateTruncating(Unsafe.Add(ref inputY, i))) * (Unsafe.Add(ref matrixY, i) * yScale);
            float restoredB = bBias.Invoke(float.CreateTruncating(Unsafe.Add(ref inputB, i))) * (Unsafe.Add(ref matrixB, i) * bScale);
            Unsafe.Add(ref outputX, i) = float.MultiplyAddEstimate(xCorrelation, restoredY, restoredX);
            Unsafe.Add(ref outputY, i) = restoredY;
            Unsafe.Add(ref outputB, i) = float.MultiplyAddEstimate(bCorrelation, restoredY, restoredB);
        }
    }

    /// <summary>
    /// Numerically converts 4 signed coefficients without reading beyond those coefficients.
    /// </summary>
    /// <typeparam name="TCoefficient">The short or int storage selected at the owning entry point.</typeparam>
    /// <param name="source">The start of a coefficient plane.</param>
    /// <param name="offset">The coefficient offset, in storage elements.</param>
    /// <returns>Float values in their original frequency order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Load128<TCoefficient>(ref TCoefficient source, uint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The closed short/int worker makes this storage-width choice constant. It adds no per-frequency type dispatch.
        // Short storage needs half as many bytes as the float result. Load only that half, then sign-extend its low lanes.
        if (Unsafe.SizeOf<TCoefficient>() == sizeof(short))
        {
            Vector64<short> packed = Vector64.LoadUnsafe(ref Unsafe.As<TCoefficient, short>(ref source), offset);
            return Vector128.ConvertToSingle(Vector128.WidenLower(packed.ToVector128()));
        }

        return Vector128.ConvertToSingle(Vector128.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset));
    }

    /// <summary>
    /// Numerically converts 8 signed coefficients without reading beyond those coefficients.
    /// </summary>
    /// <typeparam name="TCoefficient">The short or int storage selected at the owning entry point.</typeparam>
    /// <param name="source">The start of a coefficient plane.</param>
    /// <param name="offset">The coefficient offset, in storage elements.</param>
    /// <returns>Float values in their original frequency order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> Load256<TCoefficient>(ref TCoefficient source, uint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The closed short/int worker makes this storage-width choice constant. It adds no per-frequency type dispatch.
        // Short storage needs half as many bytes as the float result. Load only that half, then sign-extend its low lanes.
        if (Unsafe.SizeOf<TCoefficient>() == sizeof(short))
        {
            Vector128<short> packed = Vector128.LoadUnsafe(ref Unsafe.As<TCoefficient, short>(ref source), offset);
            return Vector256.ConvertToSingle(Vector256_.Widen(packed));
        }

        return Vector256.ConvertToSingle(Vector256.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset));
    }

    /// <summary>
    /// Numerically converts 16 signed coefficients without reading beyond those coefficients.
    /// </summary>
    /// <typeparam name="TCoefficient">The short or int storage selected at the owning entry point.</typeparam>
    /// <param name="source">The start of a coefficient plane.</param>
    /// <param name="offset">The coefficient offset, in storage elements.</param>
    /// <returns>Float values in their original frequency order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> Load512<TCoefficient>(ref TCoefficient source, uint offset)
        where TCoefficient : unmanaged, IBinaryInteger<TCoefficient>
    {
        // The closed short/int worker makes this storage-width choice constant. It adds no per-frequency type dispatch.
        // Short storage needs half as many bytes as the float result. Load only that half, then sign-extend its low lanes.
        if (Unsafe.SizeOf<TCoefficient>() == sizeof(short))
        {
            Vector256<short> packed = Vector256.LoadUnsafe(ref Unsafe.As<TCoefficient, short>(ref source), offset);
            return Vector512.ConvertToSingle(Vector512.WidenLower(packed.ToVector512()));
        }

        return Vector512.ConvertToSingle(Vector512.LoadUnsafe(ref Unsafe.As<TCoefficient, int>(ref source), offset));
    }
}
