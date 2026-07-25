// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc;

/// <summary>
/// Converts integer ICC lookup-table entries to their normalized single-precision representation.
/// </summary>
internal static class IccLutNormalizer
{
    /// <summary>
    /// Defines the scalar and SIMD conversion for an integer lookup-table element type.
    /// </summary>
    /// <typeparam name="T">The integer element type.</typeparam>
    private interface INormalizeOperator<T>
        where T : unmanaged
    {
        /// <summary>
        /// Gets the divisor that maps the integer range to <c>[0, 1]</c>.
        /// </summary>
        public static abstract float Divisor { get; }

        /// <summary>
        /// Converts one scalar value.
        /// </summary>
        /// <param name="source">The integer value.</param>
        /// <returns>The normalized value.</returns>
        public static abstract float Invoke(T source);

        /// <summary>
        /// Converts one 128-bit input vector and stores the expanded single-precision results.
        /// </summary>
        /// <param name="source">The packed integer values.</param>
        /// <param name="divisor">The normalization divisor.</param>
        /// <param name="destination">The first destination element.</param>
        public static abstract void Invoke(Vector128<T> source, Vector128<float> divisor, ref float destination);

        /// <summary>
        /// Converts one 256-bit input vector and stores the expanded single-precision results.
        /// </summary>
        /// <param name="source">The packed integer values.</param>
        /// <param name="divisor">The normalization divisor.</param>
        /// <param name="destination">The first destination element.</param>
        public static abstract void Invoke(Vector256<T> source, Vector256<float> divisor, ref float destination);

        /// <summary>
        /// Converts one 512-bit input vector and stores the expanded single-precision results.
        /// </summary>
        /// <param name="source">The packed integer values.</param>
        /// <param name="divisor">The normalization divisor.</param>
        /// <param name="destination">The first destination element.</param>
        public static abstract void Invoke(Vector512<T> source, Vector512<float> divisor, ref float destination);
    }

    /// <summary>
    /// Converts byte lookup-table entries to normalized single-precision values.
    /// </summary>
    /// <param name="source">The integer lookup-table entries.</param>
    /// <param name="destination">The normalized destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Normalize(ReadOnlySpan<byte> source, Span<float> destination)
        => Normalize<byte, ByteNormalizeOperator>(source, destination);

    /// <summary>
    /// Converts unsigned-short lookup-table entries to normalized single-precision values.
    /// </summary>
    /// <param name="source">The integer lookup-table entries.</param>
    /// <param name="destination">The normalized destination values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Normalize(ReadOnlySpan<ushort> source, Span<float> destination)
        => Normalize<ushort, UInt16NormalizeOperator>(source, destination);

    /// <summary>
    /// Converts an integer lookup table using the widest available portable SIMD width, followed by narrower
    /// widths and a scalar remainder.
    /// </summary>
    /// <typeparam name="T">The integer element type.</typeparam>
    /// <typeparam name="TOperator">The conversion implementation.</typeparam>
    /// <param name="source">The integer lookup-table entries.</param>
    /// <param name="destination">The normalized destination values.</param>
    private static void Normalize<T, TOperator>(ReadOnlySpan<T> source, Span<float> destination)
        where T : unmanaged
        where TOperator : struct, INormalizeOperator<T>
    {
        ref T sourceRef = ref MemoryMarshal.GetReference(source);
        ref float destinationRef = ref MemoryMarshal.GetReference(destination);
        nuint length = (uint)source.Length;
        nuint index = 0;

        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<float> divisor = Vector512.Create(TOperator.Divisor);
            nuint count = (uint)Vector512<T>.Count;

            while (length - index >= count)
            {
                ref float destinationStart = ref Unsafe.Add(ref destinationRef, index);
                TOperator.Invoke(Vector512.LoadUnsafe(ref sourceRef, index), divisor, ref destinationStart);
                index += count;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> divisor = Vector256.Create(TOperator.Divisor);
            nuint count = (uint)Vector256<T>.Count;

            while (length - index >= count)
            {
                ref float destinationStart = ref Unsafe.Add(ref destinationRef, index);
                TOperator.Invoke(Vector256.LoadUnsafe(ref sourceRef, index), divisor, ref destinationStart);
                index += count;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> divisor = Vector128.Create(TOperator.Divisor);
            nuint count = (uint)Vector128<T>.Count;

            while (length - index >= count)
            {
                ref float destinationStart = ref Unsafe.Add(ref destinationRef, index);
                TOperator.Invoke(Vector128.LoadUnsafe(ref sourceRef, index), divisor, ref destinationStart);
                index += count;
            }
        }

        // Preserve the scalar division expression for the final partial vector. Multiplication by a reciprocal
        // is not bit-equivalent for every input and would change the values stored in the ICC profile model.
        while (index < length)
        {
            Unsafe.Add(ref destinationRef, index) = TOperator.Invoke(Unsafe.Add(ref sourceRef, index));
            index++;
        }
    }

    /// <summary>
    /// Converts packed byte entries to normalized single-precision values.
    /// </summary>
    private readonly struct ByteNormalizeOperator : INormalizeOperator<byte>
    {
        /// <inheritdoc/>
        public static float Divisor => byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Invoke(byte source)
            => source / (float)byte.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector128<byte> source, Vector128<float> divisor, ref float destination)
        {
            // [b0..b15] becomes four ordered groups of four UInt32 values. Every widened value is at most
            // 255, so reinterpreting UInt32 as Int32 before conversion preserves its numeric value.
            (Vector128<ushort> lower16, Vector128<ushort> upper16) = Vector128.Widen(source);
            (Vector128<uint> values0, Vector128<uint> values1) = Vector128.Widen(lower16);
            (Vector128<uint> values2, Vector128<uint> values3) = Vector128.Widen(upper16);

            (Vector128.ConvertToSingle(values0.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector128.ConvertToSingle(values1.AsInt32()) / divisor).StoreUnsafe(ref destination, 4);
            (Vector128.ConvertToSingle(values2.AsInt32()) / divisor).StoreUnsafe(ref destination, 8);
            (Vector128.ConvertToSingle(values3.AsInt32()) / divisor).StoreUnsafe(ref destination, 12);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector256<byte> source, Vector256<float> divisor, ref float destination)
        {
            // [b0..b31] becomes four ordered groups of eight UInt32 values, matching four contiguous
            // Vector256<float> stores without shuffling the converted results.
            (Vector256<ushort> lower16, Vector256<ushort> upper16) = Vector256.Widen(source);
            (Vector256<uint> values0, Vector256<uint> values1) = Vector256.Widen(lower16);
            (Vector256<uint> values2, Vector256<uint> values3) = Vector256.Widen(upper16);

            (Vector256.ConvertToSingle(values0.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector256.ConvertToSingle(values1.AsInt32()) / divisor).StoreUnsafe(ref destination, 8);
            (Vector256.ConvertToSingle(values2.AsInt32()) / divisor).StoreUnsafe(ref destination, 16);
            (Vector256.ConvertToSingle(values3.AsInt32()) / divisor).StoreUnsafe(ref destination, 24);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector512<byte> source, Vector512<float> divisor, ref float destination)
        {
            // [b0..b63] becomes four ordered groups of sixteen UInt32 values, matching four contiguous
            // Vector512<float> stores. The portable widening APIs map to zero-extension instructions.
            (Vector512<ushort> lower16, Vector512<ushort> upper16) = Vector512.Widen(source);
            (Vector512<uint> values0, Vector512<uint> values1) = Vector512.Widen(lower16);
            (Vector512<uint> values2, Vector512<uint> values3) = Vector512.Widen(upper16);

            (Vector512.ConvertToSingle(values0.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector512.ConvertToSingle(values1.AsInt32()) / divisor).StoreUnsafe(ref destination, 16);
            (Vector512.ConvertToSingle(values2.AsInt32()) / divisor).StoreUnsafe(ref destination, 32);
            (Vector512.ConvertToSingle(values3.AsInt32()) / divisor).StoreUnsafe(ref destination, 48);
        }
    }

    /// <summary>
    /// Converts packed unsigned-short entries to normalized single-precision values.
    /// </summary>
    private readonly struct UInt16NormalizeOperator : INormalizeOperator<ushort>
    {
        /// <inheritdoc/>
        public static float Divisor => ushort.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Invoke(ushort source)
            => source / (float)ushort.MaxValue;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector128<ushort> source, Vector128<float> divisor, ref float destination)
        {
            // [u0..u7] becomes two ordered groups of four UInt32 values. Every value is at most 65535,
            // so signed conversion after reinterpretation is numerically identical to unsigned conversion.
            (Vector128<uint> lower, Vector128<uint> upper) = Vector128.Widen(source);

            (Vector128.ConvertToSingle(lower.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector128.ConvertToSingle(upper.AsInt32()) / divisor).StoreUnsafe(ref destination, 4);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector256<ushort> source, Vector256<float> divisor, ref float destination)
        {
            // [u0..u15] becomes two ordered groups of eight UInt32 values, matching two contiguous
            // Vector256<float> stores without a result shuffle.
            (Vector256<uint> lower, Vector256<uint> upper) = Vector256.Widen(source);

            (Vector256.ConvertToSingle(lower.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector256.ConvertToSingle(upper.AsInt32()) / divisor).StoreUnsafe(ref destination, 8);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(Vector512<ushort> source, Vector512<float> divisor, ref float destination)
        {
            // [u0..u31] becomes two ordered groups of sixteen UInt32 values, matching two contiguous
            // Vector512<float> stores. The portable widening APIs map to zero-extension instructions.
            (Vector512<uint> lower, Vector512<uint> upper) = Vector512.Widen(source);

            (Vector512.ConvertToSingle(lower.AsInt32()) / divisor).StoreUnsafe(ref destination);
            (Vector512.ConvertToSingle(upper.AsInt32()) / divisor).StoreUnsafe(ref destination, 16);
        }
    }
}
