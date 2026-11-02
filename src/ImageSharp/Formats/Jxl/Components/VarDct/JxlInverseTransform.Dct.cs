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
    /// <param name="scratch">The loaded values followed by recursive butterfly storage.</param>
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
        Span<TLanes> storage = MemoryMarshal.Cast<float, TLanes>(scratch);
        Span<TLanes> values = storage[..length];
        Span<TLanes> temporary = storage[length..];
        ref float input = ref MemoryMarshal.GetReference(source);
        ref float output = ref MemoryMarshal.GetReference(destination);
        int lanes = Unsafe.SizeOf<TLanes>() / sizeof(float);
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
    /// Applies the inverse radix-two network in place. Recursive calls share the unused suffix of temporary storage.
    /// </summary>
    /// <typeparam name="TLanes">The independent column lanes.</typeparam>
    /// <typeparam name="TOperator">The arithmetic implementation.</typeparam>
    /// <param name="values">The frequency values, then spatial values.</param>
    /// <param name="temporary">At least twice the number of values.</param>
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
