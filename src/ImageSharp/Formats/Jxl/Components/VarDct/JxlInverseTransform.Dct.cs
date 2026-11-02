// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

/// <content>
/// Contains the separable radix-two DCT. A SIMD lane carries one independent column through the complete butterfly network.
/// </content>
internal static partial class JxlInverseTransform
{
    /// <summary>
    /// Defines lane-local arithmetic and unaligned memory access for the shared transform network.
    /// </summary>
    /// <typeparam name="TLanes">The scalar or SIMD lane storage.</typeparam>
    private interface ILaneOperator<TLanes>
        where TLanes : unmanaged
    {
        /// <summary>
        /// Loads one batch of consecutive column values.
        /// </summary>
        /// <param name="source">The first column value.</param>
        /// <returns>The loaded lanes.</returns>
        public static abstract TLanes Load(ref float source);

        /// <summary>
        /// Stores one batch of consecutive column values.
        /// </summary>
        /// <param name="value">The output lanes.</param>
        /// <param name="destination">The first output column.</param>
        public static abstract void Store(TLanes value, ref float destination);

        /// <summary>
        /// Adds corresponding lanes.
        /// </summary>
        /// <param name="left">The first addend.</param>
        /// <param name="right">The second addend.</param>
        /// <returns>The sums.</returns>
        public static abstract TLanes Add(TLanes left, TLanes right);

        /// <summary>
        /// Subtracts corresponding lanes.
        /// </summary>
        /// <param name="left">The minuend.</param>
        /// <param name="right">The subtrahend.</param>
        /// <returns>The differences.</returns>
        public static abstract TLanes Subtract(TLanes left, TLanes right);

        /// <summary>
        /// Multiplies each lane by one shared constant.
        /// </summary>
        /// <param name="value">The input lanes.</param>
        /// <param name="scale">The multiplier.</param>
        /// <returns>The products.</returns>
        public static abstract TLanes Multiply(TLanes value, float scale);

        /// <summary>
        /// Multiplies each lane by a constant and adds the corresponding addend.
        /// </summary>
        /// <param name="value">The input lanes.</param>
        /// <param name="scale">The multiplier.</param>
        /// <param name="addend">The addend lanes.</param>
        /// <returns>The multiply-add results.</returns>
        public static abstract TLanes MultiplyAdd(TLanes value, float scale, TLanes addend);
    }

    /// <summary>
    /// Reconstructs a complete eight-by-eight DCT block with both axes retained in register values.
    /// </summary>
    /// <param name="source">The 64 normalized coefficients, including the restored DC value.</param>
    /// <param name="destination">The complete spatial block. The caller clips visible image edges after reconstruction.</param>
    /// <param name="stride">The destination row stride in floats.</param>
    private static void InverseDct8By8(ReadOnlySpan<float> source, Span<float> destination, int stride)
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);

        // Eight float columns fill one Vector256 exactly. Each register holds one frequency row;
        // its lanes carry independent columns through the same eight-point butterfly as the general path.
        // A Vector512 would require padding outside this block. Smaller SIMD widths retain the general path.
        Vector256<float> v0 = Vector256.LoadUnsafe(ref input);
        Vector256<float> v1 = Vector256.LoadUnsafe(ref input, 8U);
        Vector256<float> v2 = Vector256.LoadUnsafe(ref input, 16U);
        Vector256<float> v3 = Vector256.LoadUnsafe(ref input, 24U);
        Vector256<float> v4 = Vector256.LoadUnsafe(ref input, 32U);
        Vector256<float> v5 = Vector256.LoadUnsafe(ref input, 40U);
        Vector256<float> v6 = Vector256.LoadUnsafe(ref input, 48U);
        Vector256<float> v7 = Vector256.LoadUnsafe(ref input, 56U);
        Inverse8<Vector256<float>, Vector256Operator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // The first axis, transpose and second axis have the same order as InverseDct. The shared
        // transpose exchanges 32-bit lanes without arithmetic; these ref casts retain every float bit.
        // Keep the first-axis output here rather than storing and reloading a separate transpose plane.
        SimdUtils.Transpose(
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v0),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v1),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v2),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v3),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v4),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v5),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v6),
            ref Unsafe.As<Vector256<float>, Vector256<int>>(ref v7));

        Inverse8<Vector256<float>, Vector256Operator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // Only complete output rows are stored. The same strided destination contract serves the
        // contiguous reconstruction workspace and leaves any destination-row padding unchanged.
        v0.StoreUnsafe(ref output);
        v1.StoreUnsafe(ref output, (nuint)stride);
        v2.StoreUnsafe(ref output, (nuint)(2 * stride));
        v3.StoreUnsafe(ref output, (nuint)(3 * stride));
        v4.StoreUnsafe(ref output, (nuint)(4 * stride));
        v5.StoreUnsafe(ref output, (nuint)(5 * stride));
        v6.StoreUnsafe(ref output, (nuint)(6 * stride));
        v7.StoreUnsafe(ref output, (nuint)(7 * stride));
    }

    /// <summary>
    /// Transforms contiguous columns along one axis. Source and destination must not overlap.
    /// </summary>
    /// <param name="source">The input matrix from its logical origin.</param>
    /// <param name="sourceStride">The source row stride in floats.</param>
    /// <param name="destination">The output matrix from its logical origin.</param>
    /// <param name="destinationStride">The destination row stride in floats.</param>
    /// <param name="length">The transform axis length, a power of two from one through 256.</param>
    /// <param name="columns">The number of independent columns.</param>
    /// <param name="scratch">The caller-owned butterfly storage, at least 48 times the axis length.</param>
    /// <param name="inverse">Whether to reconstruct spatial values instead of normalized coefficients.</param>
    private static void TransformAxis(
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        int length,
        int columns,
        Span<float> scratch,
        bool inverse)
    {
        // Register lanes never cross columns. The same butterfly order therefore applies to every supported SIMD width and the scalar tail.
        int start = 0;
        if (Vector512.IsHardwareAccelerated && columns >= Vector512<float>.Count)
        {
            int count = (int)columns.Vector512Count<float>() * Vector512<float>.Count;
            TransformColumns<Vector512<float>, Vector512Operator>(source, sourceStride, destination, destinationStride, length, start, count, scratch, inverse);
            start += count;
        }

        if (Vector256.IsHardwareAccelerated && columns - start >= Vector256<float>.Count)
        {
            int count = (int)(columns - start).Vector256Count<float>() * Vector256<float>.Count;
            TransformColumns<Vector256<float>, Vector256Operator>(source, sourceStride, destination, destinationStride, length, start, count, scratch, inverse);
            start += count;
        }

        if (Vector128.IsHardwareAccelerated && columns - start >= Vector128<float>.Count)
        {
            int count = (int)(columns - start).Vector128Count<float>() * Vector128<float>.Count;
            TransformColumns<Vector128<float>, Vector128Operator>(source, sourceStride, destination, destinationStride, length, start, count, scratch, inverse);
            start += count;
        }

        if (start < columns)
        {
            TransformColumns<float, ScalarOperator>(source, sourceStride, destination, destinationStride, length, start, columns - start, scratch, inverse);
        }
    }

    /// <summary>
    /// Loads and transforms complete batches of independent columns with one arithmetic implementation.
    /// </summary>
    /// <typeparam name="TLanes">The scalar or SIMD lane storage.</typeparam>
    /// <typeparam name="TOperator">The arithmetic for that storage.</typeparam>
    /// <param name="source">The input matrix.</param>
    /// <param name="sourceStride">The source row stride.</param>
    /// <param name="destination">The output matrix.</param>
    /// <param name="destinationStride">The output row stride.</param>
    /// <param name="length">The transform axis length.</param>
    /// <param name="start">The first column.</param>
    /// <param name="count">The number of columns, a multiple of the lane count.</param>
    /// <param name="scratch">The loaded values followed by recursive storage. Small inverse axes do not use it.</param>
    /// <param name="inverse">Whether to apply the inverse transform.</param>
    private static void TransformColumns<TLanes, TOperator>(
        ReadOnlySpan<float> source,
        int sourceStride,
        Span<float> destination,
        int destinationStride,
        int length,
        int start,
        int count,
        Span<float> scratch,
        bool inverse)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        ref float input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int lanes = Unsafe.SizeOf<TLanes>() / sizeof(float);

        // Small inverse axes fit in local lane values. Read each column batch directly, keep the complete
        // butterfly in those values, then store once. No axis scratch is needed between these operations.
        // The same lane operator supplies scalar, 128-bit, 256-bit and 512-bit arithmetic.
        if (inverse && length == 8)
        {
            for (int column = start; column < start + count; column += lanes)
            {
                TLanes v0 = TOperator.Load(ref Unsafe.Add(ref input, column));
                TLanes v1 = TOperator.Load(ref Unsafe.Add(ref input, sourceStride + column));
                TLanes v2 = TOperator.Load(ref Unsafe.Add(ref input, (2 * sourceStride) + column));
                TLanes v3 = TOperator.Load(ref Unsafe.Add(ref input, (3 * sourceStride) + column));
                TLanes v4 = TOperator.Load(ref Unsafe.Add(ref input, (4 * sourceStride) + column));
                TLanes v5 = TOperator.Load(ref Unsafe.Add(ref input, (5 * sourceStride) + column));
                TLanes v6 = TOperator.Load(ref Unsafe.Add(ref input, (6 * sourceStride) + column));
                TLanes v7 = TOperator.Load(ref Unsafe.Add(ref input, (7 * sourceStride) + column));

                Inverse8<TLanes, TOperator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
                TOperator.Store(v0, ref Unsafe.Add(ref output, column));
                TOperator.Store(v1, ref Unsafe.Add(ref output, destinationStride + column));
                TOperator.Store(v2, ref Unsafe.Add(ref output, (2 * destinationStride) + column));
                TOperator.Store(v3, ref Unsafe.Add(ref output, (3 * destinationStride) + column));
                TOperator.Store(v4, ref Unsafe.Add(ref output, (4 * destinationStride) + column));
                TOperator.Store(v5, ref Unsafe.Add(ref output, (5 * destinationStride) + column));
                TOperator.Store(v6, ref Unsafe.Add(ref output, (6 * destinationStride) + column));
                TOperator.Store(v7, ref Unsafe.Add(ref output, (7 * destinationStride) + column));
            }

            return;
        }

        if (inverse && length == 4)
        {
            for (int column = start; column < start + count; column += lanes)
            {
                TLanes v0 = TOperator.Load(ref Unsafe.Add(ref input, column));
                TLanes v1 = TOperator.Load(ref Unsafe.Add(ref input, sourceStride + column));
                TLanes v2 = TOperator.Load(ref Unsafe.Add(ref input, (2 * sourceStride) + column));
                TLanes v3 = TOperator.Load(ref Unsafe.Add(ref input, (3 * sourceStride) + column));

                Inverse4<TLanes, TOperator>(ref v0, ref v1, ref v2, ref v3);
                TOperator.Store(v0, ref Unsafe.Add(ref output, column));
                TOperator.Store(v1, ref Unsafe.Add(ref output, destinationStride + column));
                TOperator.Store(v2, ref Unsafe.Add(ref output, (2 * destinationStride) + column));
                TOperator.Store(v3, ref Unsafe.Add(ref output, (3 * destinationStride) + column));
            }

            return;
        }

        if (inverse && length == 2)
        {
            for (int column = start; column < start + count; column += lanes)
            {
                TLanes first = TOperator.Load(ref Unsafe.Add(ref input, column));
                TLanes second = TOperator.Load(ref Unsafe.Add(ref input, sourceStride + column));
                TOperator.Store(TOperator.Add(first, second), ref Unsafe.Add(ref output, column));
                TOperator.Store(TOperator.Subtract(first, second), ref Unsafe.Add(ref output, destinationStride + column));
            }

            return;
        }

        Span<TLanes> storage = MemoryMarshal.Cast<float, TLanes>(scratch);
        Span<TLanes> values = storage[..length];
        Span<TLanes> temporary = storage[length..];
        for (int column = start; column < start + count; column += lanes)
        {
            for (int i = 0; i < length; i++)
            {
                values[i] = TOperator.Load(ref Unsafe.Add(ref input, (i * sourceStride) + column));
            }

            if (inverse)
            {
                Inverse<TLanes, TOperator>(values, temporary);
            }
            else
            {
                Forward<TLanes, TOperator>(values, temporary);
            }

            // Forward JPEG XL coefficients include 1/N on each axis. The inverse uses the unscaled butterfly outputs.
            float scale = inverse ? 1F : 1F / length;
            for (int i = 0; i < length; i++)
            {
                TLanes value = inverse ? values[i] : TOperator.Multiply(values[i], scale);
                TOperator.Store(value, ref Unsafe.Add(ref output, (i * destinationStride) + column));
            }
        }
    }

    /// <summary>
    /// Applies the inverse radix-two network in place, using fixed butterflies at its small leaves.
    /// </summary>
    /// <typeparam name="TLanes">The independent column lanes.</typeparam>
    /// <typeparam name="TOperator">The arithmetic implementation.</typeparam>
    /// <param name="values">The frequency values, then spatial values.</param>
    /// <param name="temporary">At least twice the number of values. Axes of eight or fewer values do not use it.</param>
    private static void Inverse<TLanes, TOperator>(Span<TLanes> values, Span<TLanes> temporary)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        int length = values.Length;
        if (length == 1)
        {
            return;
        }

        if (length == 2)
        {
            TLanes first = values[0];
            values[0] = TOperator.Add(first, values[1]);
            values[1] = TOperator.Subtract(first, values[1]);
            return;
        }

        // Larger transforms reach these same fixed leaves. Their values already occupy axis scratch,
        // but the leaf itself needs no even/odd copy or recursive call.
        ref TLanes firstValue = ref MemoryMarshal.GetReference(values);
        if (length == 4)
        {
            Inverse4<TLanes, TOperator>(
                ref firstValue, ref Unsafe.Add(ref firstValue, 1), ref Unsafe.Add(ref firstValue, 2), ref Unsafe.Add(ref firstValue, 3));

            return;
        }

        if (length == 8)
        {
            Inverse8<TLanes, TOperator>(
                ref firstValue,
                ref Unsafe.Add(ref firstValue, 1),
                ref Unsafe.Add(ref firstValue, 2),
                ref Unsafe.Add(ref firstValue, 3),
                ref Unsafe.Add(ref firstValue, 4),
                ref Unsafe.Add(ref firstValue, 5),
                ref Unsafe.Add(ref firstValue, 6),
                ref Unsafe.Add(ref firstValue, 7));

            return;
        }

        int half = length / 2;
        Span<TLanes> even = temporary[..half];
        Span<TLanes> odd = temporary.Slice(half, half);
        for (int i = 0; i < half; i++)
        {
            even[i] = values[2 * i];
            odd[i] = values[(2 * i) + 1];
        }

        Inverse<TLanes, TOperator>(even, temporary[length..]);

        // Reverse traversal retains the original preceding odd coefficient. This is the transpose of the forward adjacent-sum stage.
        for (int i = half - 1; i > 0; i--)
        {
            odd[i] = TOperator.Add(odd[i], odd[i - 1]);
        }

        odd[0] = TOperator.Multiply(odd[0], 1.4142135623730951F);
        Inverse<TLanes, TOperator>(odd, temporary[length..]);
        ReadOnlySpan<float> multipliers = GetMultipliers(length);
        for (int i = 0; i < half; i++)
        {
            values[i] = TOperator.MultiplyAdd(odd[i], multipliers[i], even[i]);
            values[length - i - 1] = TOperator.MultiplyAdd(odd[i], -multipliers[i], even[i]);
        }
    }

    /// <summary>
    /// Reconstructs four frequency values with the fixed inverse radix-two butterfly.
    /// </summary>
    /// <typeparam name="TLanes">Independent column values, with one column in each scalar or SIMD lane.</typeparam>
    /// <typeparam name="TOperator">The existing arithmetic for those lanes.</typeparam>
    /// <param name="v0">Frequency zero, replaced by spatial position zero.</param>
    /// <param name="v1">Frequency one, replaced by spatial position one.</param>
    /// <param name="v2">Frequency two, replaced by spatial position two.</param>
    /// <param name="v3">Frequency three, replaced by spatial position three.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Inverse4<TLanes, TOperator>(ref TLanes v0, ref TLanes v1, ref TLanes v2, ref TLanes v3)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        // These are IDCT1DImpl<4>'s even/odd split, two-point leaves and BTranspose stage from libjxl dct-inl.h.
        // Form every input-dependent value before replacing the distinct outputs. Addition order and rounding
        // match the general network: the preceding odd coefficient is added before the first one is scaled.
        TLanes even0 = TOperator.Add(v0, v2);
        TLanes even1 = TOperator.Subtract(v0, v2);
        TLanes adjacent = TOperator.Add(v3, v1);
        TLanes scaled = TOperator.Multiply(v1, 1.4142135623730951F);
        TLanes odd0 = TOperator.Add(scaled, adjacent);
        TLanes odd1 = TOperator.Subtract(scaled, adjacent);
        ReadOnlySpan<float> multipliers = Multipliers4;

        // Paired outputs use the same even value and opposite odd multipliers. The inverse has no 1/N scale.
        v0 = TOperator.MultiplyAdd(odd0, multipliers[0], even0);
        v3 = TOperator.MultiplyAdd(odd0, -multipliers[0], even0);
        v1 = TOperator.MultiplyAdd(odd1, multipliers[1], even1);
        v2 = TOperator.MultiplyAdd(odd1, -multipliers[1], even1);
    }

    /// <summary>
    /// Reconstructs eight frequency values with two fixed four-point butterflies and their final symmetric pairs.
    /// </summary>
    /// <typeparam name="TLanes">Independent column values, with one column in each scalar or SIMD lane.</typeparam>
    /// <typeparam name="TOperator">The existing arithmetic for those lanes.</typeparam>
    /// <param name="v0">Frequency zero, replaced by spatial position zero.</param>
    /// <param name="v1">Frequency one, replaced by spatial position one.</param>
    /// <param name="v2">Frequency two, replaced by spatial position two.</param>
    /// <param name="v3">Frequency three, replaced by spatial position three.</param>
    /// <param name="v4">Frequency four, replaced by spatial position four.</param>
    /// <param name="v5">Frequency five, replaced by spatial position five.</param>
    /// <param name="v6">Frequency six, replaced by spatial position six.</param>
    /// <param name="v7">Frequency seven, replaced by spatial position seven.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Inverse8<TLanes, TOperator>(
        ref TLanes v0, ref TLanes v1, ref TLanes v2, ref TLanes v3, ref TLanes v4, ref TLanes v5, ref TLanes v6, ref TLanes v7)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        TLanes even0 = v0;
        TLanes even1 = v2;
        TLanes even2 = v4;
        TLanes even3 = v6;
        Inverse4<TLanes, TOperator>(ref even0, ref even1, ref even2, ref even3);

        // BTranspose adds each original odd coefficient to its predecessor. Separate locals retain those
        // originals without a temporary span. Each lane follows the native eight-point network independently.
        TLanes odd0 = TOperator.Multiply(v1, 1.4142135623730951F);
        TLanes odd1 = TOperator.Add(v3, v1);
        TLanes odd2 = TOperator.Add(v5, v3);
        TLanes odd3 = TOperator.Add(v7, v5);
        Inverse4<TLanes, TOperator>(ref odd0, ref odd1, ref odd2, ref odd3);
        ReadOnlySpan<float> multipliers = Multipliers8;

        // The final stage joins even and odd frequencies into reversed output pairs, with no normalization.
        v0 = TOperator.MultiplyAdd(odd0, multipliers[0], even0);
        v7 = TOperator.MultiplyAdd(odd0, -multipliers[0], even0);
        v1 = TOperator.MultiplyAdd(odd1, multipliers[1], even1);
        v6 = TOperator.MultiplyAdd(odd1, -multipliers[1], even1);
        v2 = TOperator.MultiplyAdd(odd2, multipliers[2], even2);
        v5 = TOperator.MultiplyAdd(odd2, -multipliers[2], even2);
        v3 = TOperator.MultiplyAdd(odd3, multipliers[3], even3);
        v4 = TOperator.MultiplyAdd(odd3, -multipliers[3], even3);
    }

    /// <summary>
    /// Applies the forward radix-two network without its final normalization.
    /// </summary>
    /// <typeparam name="TLanes">The independent column lanes.</typeparam>
    /// <typeparam name="TOperator">The arithmetic implementation.</typeparam>
    /// <param name="values">The spatial values, then frequency values.</param>
    /// <param name="temporary">At least twice the number of values.</param>
    private static void Forward<TLanes, TOperator>(Span<TLanes> values, Span<TLanes> temporary)
        where TLanes : unmanaged
        where TOperator : struct, ILaneOperator<TLanes>
    {
        int length = values.Length;
        if (length == 1)
        {
            return;
        }

        if (length == 2)
        {
            TLanes first = values[0];
            values[0] = TOperator.Add(first, values[1]);
            values[1] = TOperator.Subtract(first, values[1]);
            return;
        }

        int half = length / 2;
        Span<TLanes> even = temporary[..half];
        Span<TLanes> odd = temporary.Slice(half, half);
        ReadOnlySpan<float> multipliers = GetMultipliers(length);
        for (int i = 0; i < half; i++)
        {
            even[i] = TOperator.Add(values[i], values[length - i - 1]);
            odd[i] = TOperator.Multiply(TOperator.Subtract(values[i], values[length - i - 1]), multipliers[i]);
        }

        Forward<TLanes, TOperator>(even, temporary[length..]);
        Forward<TLanes, TOperator>(odd, temporary[length..]);

        // Increasing traversal preserves the next coefficient until it has been used. The final odd coefficient is unchanged.
        odd[0] = TOperator.MultiplyAdd(odd[0], 1.4142135623730951F, odd[1]);
        for (int i = 1; i < half - 1; i++)
        {
            odd[i] = TOperator.Add(odd[i], odd[i + 1]);
        }

        for (int i = 0; i < half; i++)
        {
            values[2 * i] = even[i];
            values[(2 * i) + 1] = odd[i];
        }
    }

    /// <summary>
    /// Supplies scalar arithmetic for independent transform columns.
    /// </summary>
    private readonly struct ScalarOperator : ILaneOperator<float>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Load(ref float source)
            => source;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(float value, ref float destination)
            => destination = value;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Add(float left, float right)
            => left + right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Subtract(float left, float right)
            => left - right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Multiply(float value, float scale)
            => value * scale;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MultiplyAdd(float value, float scale, float addend)
            => float.MultiplyAddEstimate(value, scale, addend);
    }

    /// <summary>
    /// Supplies 128-bit arithmetic for independent transform columns.
    /// </summary>
    private readonly struct Vector128Operator : ILaneOperator<Vector128<float>>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Load(ref float source)
            => Vector128.LoadUnsafe(ref source);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector128<float> value, ref float destination)
            => value.StoreUnsafe(ref destination);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Add(Vector128<float> left, Vector128<float> right)
            => left + right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Subtract(Vector128<float> left, Vector128<float> right)
            => left - right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> Multiply(Vector128<float> value, float scale)
            => value * Vector128.Create(scale);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<float> MultiplyAdd(Vector128<float> value, float scale, Vector128<float> addend)
            => Vector128.MultiplyAddEstimate(value, Vector128.Create(scale), addend);
    }

    /// <summary>
    /// Supplies 256-bit arithmetic for independent transform columns.
    /// </summary>
    private readonly struct Vector256Operator : ILaneOperator<Vector256<float>>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Load(ref float source)
            => Vector256.LoadUnsafe(ref source);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector256<float> value, ref float destination)
            => value.StoreUnsafe(ref destination);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Add(Vector256<float> left, Vector256<float> right)
            => left + right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Subtract(Vector256<float> left, Vector256<float> right)
            => left - right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> Multiply(Vector256<float> value, float scale)
            => value * Vector256.Create(scale);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<float> MultiplyAdd(Vector256<float> value, float scale, Vector256<float> addend)
            => Vector256.MultiplyAddEstimate(value, Vector256.Create(scale), addend);
    }

    /// <summary>
    /// Supplies 512-bit arithmetic for independent transform columns.
    /// </summary>
    private readonly struct Vector512Operator : ILaneOperator<Vector512<float>>
    {
        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Load(ref float source)
            => Vector512.LoadUnsafe(ref source);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Store(Vector512<float> value, ref float destination)
            => value.StoreUnsafe(ref destination);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Add(Vector512<float> left, Vector512<float> right)
            => left + right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Subtract(Vector512<float> left, Vector512<float> right)
            => left - right;

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> Multiply(Vector512<float> value, float scale)
            => value * Vector512.Create(scale);

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<float> MultiplyAdd(Vector512<float> value, float scale, Vector512<float> addend)
            => Vector512.MultiplyAddEstimate(value, Vector512.Create(scale), addend);
    }
}
