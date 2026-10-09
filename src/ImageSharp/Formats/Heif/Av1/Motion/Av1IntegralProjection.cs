// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Builds and compares the integral projections of eight-bit blocks with the arithmetic of an <see cref="IIntegralProjectionOperator"/>.
/// </summary>
/// <remarks>
/// Each traversal walks the widest available register first and finishes in the scalar overload.
/// </remarks>
internal static partial class Av1IntegralProjection
{
    /// <summary>
    /// Sums each column of a block and normalizes the sums.
    /// </summary>
    /// <param name="destination">Receives one normalized sum per column.</param>
    /// <param name="samples">The samples at the block origin.</param>
    /// <param name="stride">The sample row stride.</param>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows, at most 128.</param>
    /// <param name="shift">The normalization shift.</param>
    public static void ProjectColumns(Span<short> destination, ReadOnlySpan<byte> samples, int stride, int width, int height, int shift)
        => ProjectColumns<IntegralProjectionOperator>(destination, samples, stride, width, height, shift);

    /// <summary>
    /// Sums each row of a block and normalizes the sums.
    /// </summary>
    /// <param name="destination">Receives one normalized sum per row.</param>
    /// <param name="samples">The samples at the block origin.</param>
    /// <param name="stride">The sample row stride.</param>
    /// <param name="width">The number of columns, at most 128.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="shift">The normalization shift.</param>
    public static void ProjectRows(Span<short> destination, ReadOnlySpan<byte> samples, int stride, int width, int height, int shift)
        => ProjectRows<IntegralProjectionOperator>(destination, samples, stride, width, height, shift);

    /// <summary>
    /// Returns the squared error of two projections with the mean of their difference removed.
    /// </summary>
    /// <param name="reference">The reference projection, at least as long as the source projection.</param>
    /// <param name="source">The source projection. Its length is a power of two.</param>
    /// <returns>The centered squared error.</returns>
    public static int GetVariance(ReadOnlySpan<short> reference, ReadOnlySpan<short> source)
        => GetVariance<IntegralProjectionOperator>(reference, source);

    /// <summary>
    /// Traverses <see cref="ProjectColumns(Span{short}, ReadOnlySpan{byte}, int, int, int, int)"/> at descending register widths.
    /// </summary>
    /// <typeparam name="TOperator">The projection arithmetic.</typeparam>
    /// <param name="destination">Receives one normalized sum per column.</param>
    /// <param name="samples">The samples at the block origin.</param>
    /// <param name="stride">The sample row stride.</param>
    /// <param name="width">The number of columns.</param>
    /// <param name="height">The number of rows, at most 128.</param>
    /// <param name="shift">The normalization shift.</param>
    private static void ProjectColumns<TOperator>(Span<short> destination, ReadOnlySpan<byte> samples, int stride, int width, int height, int shift)
        where TOperator : struct, IIntegralProjectionOperator
    {
        ref short destinationBase = ref MemoryMarshal.GetReference(destination[..width]);
        ref byte sampleBase = ref MemoryMarshal.GetReference(samples[..(((height - 1) * stride) + width)]);
        int x = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; x <= width - Vector512<ushort>.Count; x += Vector512<ushort>.Count)
            {
                Vector512<ushort> total = Vector512<ushort>.Zero;
                for (int y = 0; y < height; y++)
                {
                    total = TOperator.AccumulateColumns(ref Unsafe.Add(ref sampleBase, y * stride), (nuint)x, total);
                }

                Vector512.ShiftRightLogical(total, shift).AsInt16().StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; x <= width - Vector256<ushort>.Count; x += Vector256<ushort>.Count)
            {
                Vector256<ushort> total = Vector256<ushort>.Zero;
                for (int y = 0; y < height; y++)
                {
                    total = TOperator.AccumulateColumns(ref Unsafe.Add(ref sampleBase, y * stride), (nuint)x, total);
                }

                Vector256.ShiftRightLogical(total, shift).AsInt16().StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; x <= width - Vector128<ushort>.Count; x += Vector128<ushort>.Count)
            {
                Vector128<ushort> total = Vector128<ushort>.Zero;
                for (int y = 0; y < height; y++)
                {
                    total = TOperator.AccumulateColumns(ref Unsafe.Add(ref sampleBase, y * stride), (nuint)x, total);
                }

                Vector128.ShiftRightLogical(total, shift).AsInt16().StoreUnsafe(ref destinationBase, (nuint)x);
            }
        }

        for (; x < width; x++)
        {
            int total = 0;
            for (int y = 0; y < height; y++)
            {
                total = TOperator.AccumulateColumns(Unsafe.Add(ref sampleBase, (y * stride) + x), total);
            }

            Unsafe.Add(ref destinationBase, x) = (short)(total >> shift);
        }
    }

    /// <summary>
    /// Traverses <see cref="ProjectRows(Span{short}, ReadOnlySpan{byte}, int, int, int, int)"/> at descending register widths.
    /// </summary>
    /// <typeparam name="TOperator">The projection arithmetic.</typeparam>
    /// <param name="destination">Receives one normalized sum per row.</param>
    /// <param name="samples">The samples at the block origin.</param>
    /// <param name="stride">The sample row stride.</param>
    /// <param name="width">The number of columns, at most 128.</param>
    /// <param name="height">The number of rows.</param>
    /// <param name="shift">The normalization shift.</param>
    private static void ProjectRows<TOperator>(Span<short> destination, ReadOnlySpan<byte> samples, int stride, int width, int height, int shift)
        where TOperator : struct, IIntegralProjectionOperator
    {
        ref short destinationBase = ref MemoryMarshal.GetReference(destination[..height]);
        ref byte sampleBase = ref MemoryMarshal.GetReference(samples[..(((height - 1) * stride) + width)]);
        for (int y = 0; y < height; y++)
        {
            ref byte row = ref Unsafe.Add(ref sampleBase, y * stride);
            Vector512<uint> total512 = Vector512<uint>.Zero;
            Vector256<uint> total256 = Vector256<uint>.Zero;
            Vector128<uint> total128 = Vector128<uint>.Zero;
            int total = 0;
            int x = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                for (; x <= width - Vector512<byte>.Count; x += Vector512<byte>.Count)
                {
                    total512 = TOperator.AccumulateRow(Vector512.LoadUnsafe(ref row, (nuint)x), total512);
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                for (; x <= width - Vector256<byte>.Count; x += Vector256<byte>.Count)
                {
                    total256 = TOperator.AccumulateRow(Vector256.LoadUnsafe(ref row, (nuint)x), total256);
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                for (; x <= width - Vector128<byte>.Count; x += Vector128<byte>.Count)
                {
                    total128 = TOperator.AccumulateRow(Vector128.LoadUnsafe(ref row, (nuint)x), total128);
                }
            }

            for (; x < width; x++)
            {
                total = TOperator.AccumulateColumns(Unsafe.Add(ref row, x), total);
            }

            // Every output is one row total, so each row reduces its lanes once.
            total256 += total512.GetLower() + total512.GetUpper();
            total128 += total256.GetLower() + total256.GetUpper();
            total += (int)Vector128.Sum(total128);
            Unsafe.Add(ref destinationBase, y) = (short)(total >> shift);
        }
    }

    /// <summary>
    /// Traverses <see cref="GetVariance(ReadOnlySpan{short}, ReadOnlySpan{short})"/> at descending register widths.
    /// </summary>
    /// <typeparam name="TOperator">The projection arithmetic.</typeparam>
    /// <param name="reference">The reference projection, at least as long as the source projection.</param>
    /// <param name="source">The source projection. Its length is a power of two.</param>
    /// <returns>The centered squared error.</returns>
    private static int GetVariance<TOperator>(ReadOnlySpan<short> reference, ReadOnlySpan<short> source)
        where TOperator : struct, IIntegralProjectionOperator
    {
        int length = source.Length;
        ref short sourceBase = ref MemoryMarshal.GetReference(source);
        ref short referenceBase = ref MemoryMarshal.GetReference(reference[..length]);
        Vector512<int> sum512 = Vector512<int>.Zero;
        Vector256<int> sum256 = Vector256<int>.Zero;
        Vector128<int> sum128 = Vector128<int>.Zero;
        Vector512<int> squares512 = Vector512<int>.Zero;
        Vector256<int> squares256 = Vector256<int>.Zero;
        Vector128<int> squares128 = Vector128<int>.Zero;
        int sum = 0;
        int squares = 0;
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            for (; i <= length - Vector512<short>.Count; i += Vector512<short>.Count)
            {
                TOperator.AccumulateMoments(
                    Vector512.LoadUnsafe(ref referenceBase, (nuint)i), Vector512.LoadUnsafe(ref sourceBase, (nuint)i), ref sum512, ref squares512);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            for (; i <= length - Vector256<short>.Count; i += Vector256<short>.Count)
            {
                TOperator.AccumulateMoments(
                    Vector256.LoadUnsafe(ref referenceBase, (nuint)i), Vector256.LoadUnsafe(ref sourceBase, (nuint)i), ref sum256, ref squares256);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= length - Vector128<short>.Count; i += Vector128<short>.Count)
            {
                TOperator.AccumulateMoments(
                    Vector128.LoadUnsafe(ref referenceBase, (nuint)i), Vector128.LoadUnsafe(ref sourceBase, (nuint)i), ref sum128, ref squares128);
            }
        }

        for (; i < length; i++)
        {
            TOperator.AccumulateMoments(Unsafe.Add(ref referenceBase, i), Unsafe.Add(ref sourceBase, i), ref sum, ref squares);
        }

        sum256 += sum512.GetLower() + sum512.GetUpper();
        sum128 += sum256.GetLower() + sum256.GetUpper();
        squares256 += squares512.GetLower() + squares512.GetUpper();
        squares128 += squares256.GetLower() + squares256.GetUpper();
        sum += Vector128.Sum(sum128);
        squares += Vector128.Sum(squares128);

        // The squared sum of a 128-value projection can need all 32 bits. Thus the code squares the magnitude in unsigned arithmetic.
        uint magnitude = (uint)Math.Abs(sum);
        return squares - (int)((magnitude * magnitude) >> BitOperations.Log2((uint)length));
    }
}
