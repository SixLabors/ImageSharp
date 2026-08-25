// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Reconstructs HEVC transform-skipped, bypassed, and differential residual blocks.
/// </summary>
internal static class HevcResidualReconstructor
{
    /// <summary>
    /// The minimum residual sample represented by the decoder reconstruction pipeline.
    /// </summary>
    private const int ResidualMinimum = short.MinValue;

    /// <summary>
    /// The maximum residual sample represented by the decoder reconstruction pipeline.
    /// </summary>
    private const int ResidualMaximum = short.MaxValue;

    /// <summary>
    /// Defines a closed transform-skip normalization operator for every SIMD width and the scalar tail.
    /// </summary>
    private interface ITransformSkipOperator
    {
        /// <summary>
        /// Normalizes sixteen transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector512<int> Invoke(Vector512<int> values, int shift);

        /// <summary>
        /// Normalizes eight transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector256<int> Invoke(Vector256<int> values, int shift);

        /// <summary>
        /// Normalizes four transform-skipped coefficients.
        /// </summary>
        /// <param name="values">The dequantized coefficients.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residuals.</returns>
        static abstract Vector128<int> Invoke(Vector128<int> values, int shift);

        /// <summary>
        /// Normalizes one transform-skipped coefficient.
        /// </summary>
        /// <param name="value">The dequantized coefficient.</param>
        /// <param name="shift">The nonnegative shift magnitude.</param>
        /// <returns>The reconstructed residual.</returns>
        static abstract int Invoke(int value, int shift);
    }

    /// <summary>
    /// Copies one transquant-bypass coefficient block into residual sample order.
    /// </summary>
    /// <param name="coefficients">The decoded coefficients in raster order.</param>
    /// <param name="residual">The destination residual block in packed raster order.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    public static void CopyBypassed(ReadOnlySpan<int> coefficients, Span<int> residual, bool rotate)
    {
        Span<int> destination = residual[..coefficients.Length];
        if (!rotate)
        {
            coefficients.CopyTo(destination);
            return;
        }

        CopyReversed(coefficients, destination);
    }

    /// <summary>
    /// Reconstructs one transform-skipped residual block from dequantized coefficients.
    /// </summary>
    /// <param name="coefficients">The dequantized coefficients in raster order.</param>
    /// <param name="residual">The destination residual block in packed raster order.</param>
    /// <param name="width">The transform-block width.</param>
    /// <param name="height">The transform-block height.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="maxTransformDynamicRange">The transform dynamic range excluding its sign bit.</param>
    /// <param name="equivalentLog2TransformSize">The base-two logarithm of the equivalent square transform size.</param>
    /// <param name="extendedPrecisionProcessingEnabled">Whether transform-skip precision is extended by the sequence.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    public static void ApplyTransformSkip(
        ReadOnlySpan<int> coefficients,
        Span<int> residual,
        int width,
        int height,
        int bitDepth,
        int maxTransformDynamicRange,
        int equivalentLog2TransformSize,
        bool extendedPrecisionProcessingEnabled,
        bool rotate)
    {
        int transformShift = maxTransformDynamicRange - bitDepth - equivalentLog2TransformSize;
        if (extendedPrecisionProcessingEnabled)
        {
            transformShift = Math.Max(0, transformShift);
        }

        int coefficientCount = width * height;
        if (transformShift >= 0)
        {
            ApplyTransformSkip<RightShiftTransformSkipOperator>(coefficients[..coefficientCount], residual[..coefficientCount], transformShift, rotate);
        }
        else
        {
            ApplyTransformSkip<LeftShiftTransformSkipOperator>(coefficients[..coefficientCount], residual[..coefficientCount], -transformShift, rotate);
        }
    }

    /// <summary>
    /// Gets whether a non-transformed residual block uses the HEVC Range Extensions coefficient rotation.
    /// </summary>
    /// <param name="transformSkipRotationEnabled">Whether the sequence enables transform-skip rotation.</param>
    /// <param name="isIntraPredicted">Whether the transform unit belongs to an intra-predicted coding unit.</param>
    /// <param name="width">The transform-block width.</param>
    /// <returns><see langword="true"/> when the complete coefficient order is reversed; otherwise, <see langword="false"/>.</returns>
    public static bool IsNonTransformedResidualRotated(bool transformSkipRotationEnabled, bool isIntraPredicted, int width)
        => transformSkipRotationEnabled && isIntraPredicted && width == 4;

    /// <summary>
    /// Gets the implicit residual differential mode selected by an intra-prediction direction.
    /// </summary>
    /// <param name="intraPredictionMode">The resolved luma or chroma intra-prediction mode.</param>
    /// <param name="remapChroma422">Whether the 4:2:2 chroma intra-angle remapping applies.</param>
    /// <returns>The residual differential mode selected by the prediction direction.</returns>
    public static HevcResidualDpcmMode GetImplicitResidualDpcmMode(int intraPredictionMode, bool remapChroma422)
    {
        int predictionMode = remapChroma422 ? HevcIntraPredictionMode.RemapChroma422(intraPredictionMode) : intraPredictionMode;
        return predictionMode switch
        {
            HevcIntraPredictionMode.Horizontal => HevcResidualDpcmMode.Horizontal,
            HevcIntraPredictionMode.Vertical => HevcResidualDpcmMode.Vertical,
            _ => HevcResidualDpcmMode.None,
        };
    }

    /// <summary>
    /// Applies inverse residual differential pulse-code modulation to one packed residual block.
    /// </summary>
    /// <param name="residual">The residual block in packed raster order.</param>
    /// <param name="width">The residual-block width.</param>
    /// <param name="height">The residual-block height.</param>
    /// <param name="mode">The differential accumulation direction.</param>
    public static void ApplyResidualDpcm(Span<int> residual, int width, int height, HevcResidualDpcmMode mode)
    {
        if (mode == HevcResidualDpcmMode.Vertical)
        {
            ApplyVerticalResidualDpcm(residual, width, height);
        }
        else if (mode == HevcResidualDpcmMode.Horizontal)
        {
            ApplyHorizontalResidualDpcm(residual, width, height);
        }
    }

    /// <summary>
    /// Applies one transform-skip normalization operator to a complete coefficient block.
    /// </summary>
    /// <typeparam name="TOperator">The signed shift operator selected before entering the hot loop.</typeparam>
    /// <param name="coefficients">The dequantized coefficients in raster order.</param>
    /// <param name="residual">The destination residual block in packed raster order.</param>
    /// <param name="shift">The nonnegative shift magnitude.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    private static void ApplyTransformSkip<TOperator>(ReadOnlySpan<int> coefficients, Span<int> residual, int shift, bool rotate)
        where TOperator : struct, ITransformSkipOperator
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(coefficients);
        ref int destinationBase = ref MemoryMarshal.GetReference(residual);
        int count = coefficients.Length;
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= count - Vector512<int>.Count; index += Vector512<int>.Count)
            {
                Vector512<int> values = Load512(ref sourceBase, count, index, rotate);
                TOperator.Invoke(values, shift).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= count - Vector256<int>.Count; index += Vector256<int>.Count)
            {
                Vector256<int> values = Load256(ref sourceBase, count, index, rotate);
                TOperator.Invoke(values, shift).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= count - Vector128<int>.Count; index += Vector128<int>.Count)
            {
                Vector128<int> values = Load128(ref sourceBase, count, index, rotate);
                TOperator.Invoke(values, shift).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        for (; index < count; index++)
        {
            int sourceIndex = rotate ? count - 1 - index : index;
            Unsafe.Add(ref destinationBase, index) = TOperator.Invoke(Unsafe.Add(ref sourceBase, sourceIndex), shift);
        }
    }

    /// <summary>
    /// Copies one coefficient block while reversing its complete raster order.
    /// </summary>
    /// <param name="source">The source coefficient block.</param>
    /// <param name="destination">The destination residual block.</param>
    private static void CopyReversed(ReadOnlySpan<int> source, Span<int> destination)
    {
        ref int sourceBase = ref MemoryMarshal.GetReference(source);
        ref int destinationBase = ref MemoryMarshal.GetReference(destination);
        int count = source.Length;
        int index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            for (; index <= count - Vector512<int>.Count; index += Vector512<int>.Count)
            {
                Load512(ref sourceBase, count, index, true).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; index <= count - Vector256<int>.Count; index += Vector256<int>.Count)
            {
                Load256(ref sourceBase, count, index, true).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; index <= count - Vector128<int>.Count; index += Vector128<int>.Count)
            {
                Load128(ref sourceBase, count, index, true).StoreUnsafe(ref destinationBase, (nuint)index);
            }
        }

        for (; index < count; index++)
        {
            Unsafe.Add(ref destinationBase, index) = Unsafe.Add(ref sourceBase, count - 1 - index);
        }
    }

    /// <summary>
    /// Loads and optionally reverses sixteen source coefficients.
    /// </summary>
    /// <param name="source">The first source coefficient.</param>
    /// <param name="count">The complete coefficient count.</param>
    /// <param name="index">The destination coefficient index.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    /// <returns>The source coefficients in destination order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> Load512(ref int source, int count, int index, bool rotate)
    {
        if (!rotate)
        {
            return Vector512.LoadUnsafe(ref source, (nuint)index);
        }

        Vector512<int> values = Vector512.LoadUnsafe(ref source, (nuint)(count - index - Vector512<int>.Count));
        return Vector512.Shuffle(values, Vector512.Create(15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0));
    }

    /// <summary>
    /// Loads and optionally reverses eight source coefficients.
    /// </summary>
    /// <param name="source">The first source coefficient.</param>
    /// <param name="count">The complete coefficient count.</param>
    /// <param name="index">The destination coefficient index.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    /// <returns>The source coefficients in destination order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Load256(ref int source, int count, int index, bool rotate)
    {
        if (!rotate)
        {
            return Vector256.LoadUnsafe(ref source, (nuint)index);
        }

        Vector256<int> values = Vector256.LoadUnsafe(ref source, (nuint)(count - index - Vector256<int>.Count));
        return Vector256.Shuffle(values, Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0));
    }

    /// <summary>
    /// Loads and optionally reverses four source coefficients.
    /// </summary>
    /// <param name="source">The first source coefficient.</param>
    /// <param name="count">The complete coefficient count.</param>
    /// <param name="index">The destination coefficient index.</param>
    /// <param name="rotate">Whether the complete coefficient order is reversed.</param>
    /// <returns>The source coefficients in destination order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Load128(ref int source, int count, int index, bool rotate)
    {
        if (!rotate)
        {
            return Vector128.LoadUnsafe(ref source, (nuint)index);
        }

        Vector128<int> values = Vector128.LoadUnsafe(ref source, (nuint)(count - index - Vector128<int>.Count));
        return Vector128.Shuffle(values, Vector128.Create(3, 2, 1, 0));
    }

    /// <summary>
    /// Accumulates residual differences from top to bottom while processing independent columns in SIMD lanes.
    /// </summary>
    /// <param name="residual">The residual block in packed raster order.</param>
    /// <param name="width">The residual-block width.</param>
    /// <param name="height">The residual-block height.</param>
    private static void ApplyVerticalResidualDpcm(Span<int> residual, int width, int height)
    {
        ref int residualBase = ref MemoryMarshal.GetReference(residual);
        int x = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<int> minimum = Vector512.Create(ResidualMinimum);
            Vector512<int> maximum = Vector512.Create(ResidualMaximum);
            for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
            {
                Vector512<int> accumulator = Vector512.LoadUnsafe(ref residualBase, (nuint)x);
                for (int y = 1; y < height; y++)
                {
                    int index = (y * width) + x;
                    accumulator += Vector512.LoadUnsafe(ref residualBase, (nuint)index);
                    Vector512.Clamp(accumulator, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)index);
                }
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<int> minimum = Vector256.Create(ResidualMinimum);
            Vector256<int> maximum = Vector256.Create(ResidualMaximum);
            for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
            {
                Vector256<int> accumulator = Vector256.LoadUnsafe(ref residualBase, (nuint)x);
                for (int y = 1; y < height; y++)
                {
                    int index = (y * width) + x;
                    accumulator += Vector256.LoadUnsafe(ref residualBase, (nuint)index);
                    Vector256.Clamp(accumulator, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)index);
                }
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<int> minimum = Vector128.Create(ResidualMinimum);
            Vector128<int> maximum = Vector128.Create(ResidualMaximum);
            for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
            {
                Vector128<int> accumulator = Vector128.LoadUnsafe(ref residualBase, (nuint)x);
                for (int y = 1; y < height; y++)
                {
                    int index = (y * width) + x;
                    accumulator += Vector128.LoadUnsafe(ref residualBase, (nuint)index);
                    Vector128.Clamp(accumulator, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)index);
                }
            }
        }

        for (; x < width; x++)
        {
            int accumulator = Unsafe.Add(ref residualBase, x);
            for (int y = 1; y < height; y++)
            {
                int index = (y * width) + x;
                accumulator += Unsafe.Add(ref residualBase, index);
                Unsafe.Add(ref residualBase, index) = Math.Clamp(accumulator, ResidualMinimum, ResidualMaximum);
            }
        }
    }

    /// <summary>
    /// Accumulates residual differences from left to right using an inclusive SIMD prefix sum for each row.
    /// </summary>
    /// <param name="residual">The residual block in packed raster order.</param>
    /// <param name="width">The residual-block width.</param>
    /// <param name="height">The residual-block height.</param>
    private static void ApplyHorizontalResidualDpcm(Span<int> residual, int width, int height)
    {
        ref int residualBase = ref MemoryMarshal.GetReference(residual);
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            int x = 0;
            int accumulator = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<int> minimum = Vector512.Create(ResidualMinimum);
                Vector512<int> maximum = Vector512.Create(ResidualMaximum);
                for (; x <= width - Vector512<int>.Count; x += Vector512<int>.Count)
                {
                    Vector512<int> values = Vector512.LoadUnsafe(ref residualBase, (nuint)(rowOffset + x));
                    values = PrefixSum(values) + Vector512.Create(accumulator);
                    accumulator = values.GetElement(Vector512<int>.Count - 1);
                    Vector512.Clamp(values, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)(rowOffset + x));
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<int> minimum = Vector256.Create(ResidualMinimum);
                Vector256<int> maximum = Vector256.Create(ResidualMaximum);
                for (; x <= width - Vector256<int>.Count; x += Vector256<int>.Count)
                {
                    Vector256<int> values = Vector256.LoadUnsafe(ref residualBase, (nuint)(rowOffset + x));
                    values = PrefixSum(values) + Vector256.Create(accumulator);
                    accumulator = values.GetElement(Vector256<int>.Count - 1);
                    Vector256.Clamp(values, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)(rowOffset + x));
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<int> minimum = Vector128.Create(ResidualMinimum);
                Vector128<int> maximum = Vector128.Create(ResidualMaximum);
                for (; x <= width - Vector128<int>.Count; x += Vector128<int>.Count)
                {
                    Vector128<int> values = Vector128.LoadUnsafe(ref residualBase, (nuint)(rowOffset + x));
                    values = PrefixSum(values) + Vector128.Create(accumulator);
                    accumulator = values.GetElement(Vector128<int>.Count - 1);
                    Vector128.Clamp(values, minimum, maximum).StoreUnsafe(ref residualBase, (nuint)(rowOffset + x));
                }
            }

            for (; x < width; x++)
            {
                int index = rowOffset + x;
                accumulator += Unsafe.Add(ref residualBase, index);
                Unsafe.Add(ref residualBase, index) = x == 0 ? accumulator : Math.Clamp(accumulator, ResidualMinimum, ResidualMaximum);
            }
        }
    }

    /// <summary>
    /// Computes an inclusive prefix sum across sixteen signed lanes.
    /// </summary>
    /// <param name="values">The residual differences.</param>
    /// <returns>The accumulated residuals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<int> PrefixSum(Vector512<int> values)
    {
        values += Vector512.Shuffle(values, Vector512.Create(16, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14));
        values += Vector512.Shuffle(values, Vector512.Create(16, 16, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13));
        values += Vector512.Shuffle(values, Vector512.Create(16, 16, 16, 16, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11));
        return values + Vector512.Shuffle(values, Vector512.Create(16, 16, 16, 16, 16, 16, 16, 16, 0, 1, 2, 3, 4, 5, 6, 7));
    }

    /// <summary>
    /// Computes an inclusive prefix sum across eight signed lanes.
    /// </summary>
    /// <param name="values">The residual differences.</param>
    /// <returns>The accumulated residuals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> PrefixSum(Vector256<int> values)
    {
        values += Vector256.Shuffle(values, Vector256.Create(8, 0, 1, 2, 3, 4, 5, 6));
        values += Vector256.Shuffle(values, Vector256.Create(8, 8, 0, 1, 2, 3, 4, 5));
        return values + Vector256.Shuffle(values, Vector256.Create(8, 8, 8, 8, 0, 1, 2, 3));
    }

    /// <summary>
    /// Computes an inclusive prefix sum across four signed lanes.
    /// </summary>
    /// <param name="values">The residual differences.</param>
    /// <returns>The accumulated residuals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> PrefixSum(Vector128<int> values)
    {
        values += Vector128.Shuffle(values, Vector128.Create(4, 0, 1, 2));
        return values + Vector128.Shuffle(values, Vector128.Create(4, 4, 0, 1));
    }

    /// <summary>
    /// Applies the rounded right shift used by ordinary transform-skip reconstruction.
    /// </summary>
    private readonly struct RightShiftTransformSkipOperator : ITransformSkipOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> values, int shift)
            => shift == 0 ? values : (values + Vector512.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> values, int shift)
            => shift == 0 ? values : (values + Vector256.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> values, int shift)
            => shift == 0 ? values : (values + Vector128.Create(1 << (shift - 1))) >> shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value, int shift) => shift == 0 ? value : (value + (1 << (shift - 1))) >> shift;
    }

    /// <summary>
    /// Applies the exact left shift used by high-bit-depth transform-skip reconstruction.
    /// </summary>
    private readonly struct LeftShiftTransformSkipOperator : ITransformSkipOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<int> Invoke(Vector512<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<int> Invoke(Vector256<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Invoke(Vector128<int> values, int shift) => values << shift;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Invoke(int value, int shift) => value << shift;
    }
}
