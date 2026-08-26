// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <content>
/// Provides shared SIMD operations used by the closed prediction operators. Each lane represents one output column;
/// planar and angular interpolation widen 16-bit references before their Q5 weighted sums, then narrow only after the
/// normative rounding shift. Horizontal prediction reuses the vertical row kernel through a caller-owned contiguous
/// block and an eight-by-eight transpose, keeping the arithmetic identical without gathering strided destination rows.
/// </content>
internal static partial class HevcIntraPredictor
{
    /// <summary>
    /// Calculates one 512-bit half of a planar prediction row.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="indices">The zero-based X coordinates.</param>
    /// <param name="left">The left reference sample for the row.</param>
    /// <param name="topRight">The top-right reference sample.</param>
    /// <param name="bottomLeft">The bottom-left reference sample.</param>
    /// <param name="topWeight">The top-reference weight.</param>
    /// <param name="bottomWeight">The bottom-left-reference weight.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="rounding">The division rounding constant.</param>
    /// <param name="shift">The division shift.</param>
    /// <returns>The predicted samples as widened lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> CalculatePlanarVector(
        Vector512<uint> top,
        Vector512<uint> indices,
        uint left,
        uint topRight,
        uint bottomLeft,
        uint topWeight,
        uint bottomWeight,
        uint size,
        uint rounding,
        int shift)
    {
        Vector512<uint> horizontal = ((Vector512.Create(size - 1) - indices) * left) + ((indices + Vector512<uint>.One) * topRight);
        Vector512<uint> vertical = (top * topWeight) + Vector512.Create(bottomLeft * bottomWeight);
        return (horizontal + vertical + Vector512.Create(rounding)) >> shift;
    }

    /// <summary>
    /// Calculates one 256-bit half of a planar prediction row.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="indices">The zero-based X coordinates.</param>
    /// <param name="left">The left reference sample for the row.</param>
    /// <param name="topRight">The top-right reference sample.</param>
    /// <param name="bottomLeft">The bottom-left reference sample.</param>
    /// <param name="topWeight">The top-reference weight.</param>
    /// <param name="bottomWeight">The bottom-left-reference weight.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="rounding">The division rounding constant.</param>
    /// <param name="shift">The division shift.</param>
    /// <returns>The predicted samples as widened lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> CalculatePlanarVector(
        Vector256<uint> top,
        Vector256<uint> indices,
        uint left,
        uint topRight,
        uint bottomLeft,
        uint topWeight,
        uint bottomWeight,
        uint size,
        uint rounding,
        int shift)
    {
        Vector256<uint> horizontal = ((Vector256.Create(size - 1) - indices) * left) + ((indices + Vector256<uint>.One) * topRight);
        Vector256<uint> vertical = (top * topWeight) + Vector256.Create(bottomLeft * bottomWeight);
        return (horizontal + vertical + Vector256.Create(rounding)) >> shift;
    }

    /// <summary>
    /// Calculates one 128-bit half of a planar prediction row.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="indices">The zero-based X coordinates.</param>
    /// <param name="left">The left reference sample for the row.</param>
    /// <param name="topRight">The top-right reference sample.</param>
    /// <param name="bottomLeft">The bottom-left reference sample.</param>
    /// <param name="topWeight">The top-reference weight.</param>
    /// <param name="bottomWeight">The bottom-left-reference weight.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="rounding">The division rounding constant.</param>
    /// <param name="shift">The division shift.</param>
    /// <returns>The predicted samples as widened lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> CalculatePlanarVector(
        Vector128<uint> top,
        Vector128<uint> indices,
        uint left,
        uint topRight,
        uint bottomLeft,
        uint topWeight,
        uint bottomWeight,
        uint size,
        uint rounding,
        int shift)
    {
        Vector128<uint> horizontal = ((Vector128.Create(size - 1) - indices) * left) + ((indices + Vector128<uint>.One) * topRight);
        Vector128<uint> vertical = (top * topWeight) + Vector128.Create(bottomLeft * bottomWeight);
        return (horizontal + vertical + Vector128.Create(rounding)) >> shift;
    }

    /// <summary>
    /// Sums reconstructed reference samples without overflowing their 16-bit storage.
    /// </summary>
    /// <param name="samples">The samples to sum.</param>
    /// <returns>The exact unsigned sum.</returns>
    private static uint SumSamples(ReadOnlySpan<ushort> samples)
    {
        ref ushort samplesBase = ref MemoryMarshal.GetReference(samples);
        uint sum = 0;
        int i = 0;

        // Widen before reduction because a complete 64-sample, 12-bit reference edge exceeds UInt16. The shared index
        // lets narrower vectors consume only the remainder from the widest available path.
        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector512<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<ushort>.Count)
            {
                (Vector512<uint> low, Vector512<uint> high) = Vector512.Widen(Vector512.LoadUnsafe(ref samplesBase, (nuint)i));
                sum += Vector512.Sum(low) + Vector512.Sum(high);
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector256<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<ushort>.Count)
            {
                (Vector256<uint> low, Vector256<uint> high) = Vector256.Widen(Vector256.LoadUnsafe(ref samplesBase, (nuint)i));
                sum += Vector256.Sum(low) + Vector256.Sum(high);
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = samples.Length - Vector128<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<ushort>.Count)
            {
                (Vector128<uint> low, Vector128<uint> high) = Vector128.Widen(Vector128.LoadUnsafe(ref samplesBase, (nuint)i));
                sum += Vector128.Sum(low) + Vector128.Sum(high);
            }
        }

        for (; i < samples.Length; i++)
        {
            sum += Unsafe.Add(ref samplesBase, i);
        }

        return sum;
    }

    /// <summary>
    /// Copies the top reference into every row and optionally filters the first column.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the vertical luma edge filter applies.</param>
    private static void PredictVertical(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int bitDepth,
        bool filterPredictionEdges)
    {
        ReadOnlySpan<ushort> row = top.Slice(1, size);
        int maximum = (1 << bitDepth) - 1;
        for (int y = 0; y < size; y++)
        {
            row.CopyTo(destination.Slice(y * destinationStride, size));
            if (filterPredictionEdges)
            {
                int sample = destination[y * destinationStride] + ((left[y + 1] - left[0]) >> 1);
                destination[y * destinationStride] = (ushort)Math.Clamp(sample, 0, maximum);
            }
        }
    }

    /// <summary>
    /// Fills each row from its left reference and optionally filters the first row.
    /// </summary>
    /// <param name="top">The top reference samples.</param>
    /// <param name="left">The left reference samples.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="bitDepth">The reconstructed component precision.</param>
    /// <param name="filterPredictionEdges">Whether the horizontal luma edge filter applies.</param>
    private static void PredictHorizontal(
        ReadOnlySpan<ushort> top,
        ReadOnlySpan<ushort> left,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int bitDepth,
        bool filterPredictionEdges)
    {
        for (int y = 0; y < size; y++)
        {
            destination.Slice(y * destinationStride, size).Fill(left[y + 1]);
        }

        if (!filterPredictionEdges)
        {
            return;
        }

        int maximum = (1 << bitDepth) - 1;
        for (int x = 0; x < size; x++)
        {
            int sample = destination[x] + ((top[x + 1] - top[0]) >> 1);
            destination[x] = (ushort)Math.Clamp(sample, 0, maximum);
        }
    }

    /// <summary>
    /// Generates a vertical-oriented angular block using contiguous SIMD interpolation within each row.
    /// </summary>
    /// <param name="main">The main reference beginning at logical index zero.</param>
    /// <param name="mainOrigin">The span index corresponding to logical reference index zero.</param>
    /// <param name="destination">The contiguous destination or transposition scratch block.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    /// <param name="angle">The signed prediction displacement in thirty-second-sample units.</param>
    private static void PredictAngularRows(
        ReadOnlySpan<ushort> main,
        int mainOrigin,
        Span<ushort> destination,
        int destinationStride,
        int size,
        int angle)
    {
        for (int y = 0, deltaPosition = angle; y < size; y++, deltaPosition += angle)
        {
            int deltaInteger = deltaPosition >> 5;
            int deltaFraction = deltaPosition & 31;
            int sourceOffset = mainOrigin + deltaInteger + 1;
            Span<ushort> row = destination.Slice(y * destinationStride, size);
            if (deltaFraction == 0)
            {
                main.Slice(sourceOffset, size).CopyTo(row);
            }
            else
            {
                InterpolateAngularRow(main[sourceOffset..], row, deltaFraction);
            }
        }
    }

    /// <summary>
    /// Interpolates one angular prediction row between consecutive main-reference samples.
    /// </summary>
    /// <param name="source">The first main-reference sample for the row.</param>
    /// <param name="destination">The destination prediction row.</param>
    /// <param name="fraction">The right-hand weight with a denominator of thirty-two.</param>
    private static void InterpolateAngularRow(ReadOnlySpan<ushort> source, Span<ushort> destination, int fraction)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        uint leftWeight = (uint)(32 - fraction);
        uint rightWeight = (uint)fraction;
        int i = 0;

        // Adjacent source vectors overlap by one sample, aligning each left/right reference pair in the same lane.
        // Widening keeps the largest 12-bit Q5 weighted sum below the UInt32 limit before narrowing to sample storage.
        if (Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = destination.Length - Vector512<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector512<ushort>.Count)
            {
                Vector512<ushort> left = Vector512.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector512<ushort> right = Vector512.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector512<uint> leftLow, Vector512<uint> leftHigh) = Vector512.Widen(left);
                (Vector512<uint> rightLow, Vector512<uint> rightHigh) = Vector512.Widen(right);
                Vector512<uint> low = ((leftLow * leftWeight) + (rightLow * rightWeight) + Vector512.Create(16U)) >> 5;
                Vector512<uint> high = ((leftHigh * leftWeight) + (rightHigh * rightWeight) + Vector512.Create(16U)) >> 5;
                Vector512.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = destination.Length - Vector256<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector256<ushort>.Count)
            {
                Vector256<ushort> left = Vector256.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector256<ushort> right = Vector256.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector256<uint> leftLow, Vector256<uint> leftHigh) = Vector256.Widen(left);
                (Vector256<uint> rightLow, Vector256<uint> rightHigh) = Vector256.Widen(right);
                Vector256<uint> low = ((leftLow * leftWeight) + (rightLow * rightWeight) + Vector256.Create(16U)) >> 5;
                Vector256<uint> high = ((leftHigh * leftWeight) + (rightHigh * rightWeight) + Vector256.Create(16U)) >> 5;
                Vector256.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = destination.Length - Vector128<ushort>.Count;
            for (; i <= oneVectorFromEnd; i += Vector128<ushort>.Count)
            {
                Vector128<ushort> left = Vector128.LoadUnsafe(ref sourceBase, (nuint)i);
                Vector128<ushort> right = Vector128.LoadUnsafe(ref sourceBase, (nuint)(i + 1));
                (Vector128<uint> leftLow, Vector128<uint> leftHigh) = Vector128.Widen(left);
                (Vector128<uint> rightLow, Vector128<uint> rightHigh) = Vector128.Widen(right);
                Vector128<uint> low = ((leftLow * leftWeight) + (rightLow * rightWeight) + Vector128.Create(16U)) >> 5;
                Vector128<uint> high = ((leftHigh * leftWeight) + (rightHigh * rightWeight) + Vector128.Create(16U)) >> 5;
                Vector128.Narrow(low, high).StoreUnsafe(ref Unsafe.Add(ref destinationBase, i));
            }
        }

        for (; i < destination.Length; i++)
        {
            Unsafe.Add(ref destinationBase, i) = (ushort)(((source[i] * leftWeight) + (source[i + 1] * rightWeight) + 16) >> 5);
        }
    }

    /// <summary>
    /// Transposes a square horizontal prediction block into the reconstructed destination.
    /// </summary>
    /// <param name="source">The contiguous transposed prediction block.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="size">The square block side.</param>
    private static void TransposeBlock(ReadOnlySpan<ushort> source, Span<ushort> destination, int destinationStride, int size)
    {
        if (Vector128.IsHardwareAccelerated && size >= Vector128<ushort>.Count)
        {
            for (int y = 0; y < size; y += Vector128<ushort>.Count)
            {
                for (int x = 0; x < size; x += Vector128<ushort>.Count)
                {
                    Transpose8x8(source, destination, destinationStride, size, x, y);
                }
            }

            return;
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                destination[(x * destinationStride) + y] = source[(y * size) + x];
            }
        }
    }

    /// <summary>
    /// Transposes one eight-by-eight tile of 16-bit prediction samples.
    /// </summary>
    /// <param name="source">The contiguous source block.</param>
    /// <param name="destination">The destination block origin.</param>
    /// <param name="destinationStride">The destination row stride.</param>
    /// <param name="sourceStride">The contiguous source row stride.</param>
    /// <param name="x">The tile X coordinate in the source block.</param>
    /// <param name="y">The tile Y coordinate in the source block.</param>
    private static void Transpose8x8(
        ReadOnlySpan<ushort> source,
        Span<ushort> destination,
        int destinationStride,
        int sourceStride,
        int x,
        int y)
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Vector128<short> row0 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 0) * sourceStride) + x)).AsInt16();
        Vector128<short> row1 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 1) * sourceStride) + x)).AsInt16();
        Vector128<short> row2 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 2) * sourceStride) + x)).AsInt16();
        Vector128<short> row3 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 3) * sourceStride) + x)).AsInt16();
        Vector128<short> row4 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 4) * sourceStride) + x)).AsInt16();
        Vector128<short> row5 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 5) * sourceStride) + x)).AsInt16();
        Vector128<short> row6 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 6) * sourceStride) + x)).AsInt16();
        Vector128<short> row7 = Vector128.LoadUnsafe(ref sourceBase, (nuint)(((y + 7) * sourceStride) + x)).AsInt16();

        // Three zip stages exchange one, two, then four 16-bit coordinates. The resulting vectors are the eight
        // source columns in row order, so each can be stored contiguously into one destination row.
        Vector128<short> pair0 = Vector128_.UnpackLow(row0, row1);
        Vector128<short> pair1 = Vector128_.UnpackHigh(row0, row1);
        Vector128<short> pair2 = Vector128_.UnpackLow(row2, row3);
        Vector128<short> pair3 = Vector128_.UnpackHigh(row2, row3);
        Vector128<short> pair4 = Vector128_.UnpackLow(row4, row5);
        Vector128<short> pair5 = Vector128_.UnpackHigh(row4, row5);
        Vector128<short> pair6 = Vector128_.UnpackLow(row6, row7);
        Vector128<short> pair7 = Vector128_.UnpackHigh(row6, row7);
        Vector128<int> quad0 = Vector128_.UnpackLow(pair0.AsInt32(), pair2.AsInt32());
        Vector128<int> quad1 = Vector128_.UnpackHigh(pair0.AsInt32(), pair2.AsInt32());
        Vector128<int> quad2 = Vector128_.UnpackLow(pair1.AsInt32(), pair3.AsInt32());
        Vector128<int> quad3 = Vector128_.UnpackHigh(pair1.AsInt32(), pair3.AsInt32());
        Vector128<int> quad4 = Vector128_.UnpackLow(pair4.AsInt32(), pair6.AsInt32());
        Vector128<int> quad5 = Vector128_.UnpackHigh(pair4.AsInt32(), pair6.AsInt32());
        Vector128<int> quad6 = Vector128_.UnpackLow(pair5.AsInt32(), pair7.AsInt32());
        Vector128<int> quad7 = Vector128_.UnpackHigh(pair5.AsInt32(), pair7.AsInt32());
        Vector128<ushort> column0 = Vector128_.UnpackLow(quad0.AsInt64(), quad4.AsInt64()).AsUInt16();
        Vector128<ushort> column1 = Vector128_.UnpackHigh(quad0.AsInt64(), quad4.AsInt64()).AsUInt16();
        Vector128<ushort> column2 = Vector128_.UnpackLow(quad1.AsInt64(), quad5.AsInt64()).AsUInt16();
        Vector128<ushort> column3 = Vector128_.UnpackHigh(quad1.AsInt64(), quad5.AsInt64()).AsUInt16();
        Vector128<ushort> column4 = Vector128_.UnpackLow(quad2.AsInt64(), quad6.AsInt64()).AsUInt16();
        Vector128<ushort> column5 = Vector128_.UnpackHigh(quad2.AsInt64(), quad6.AsInt64()).AsUInt16();
        Vector128<ushort> column6 = Vector128_.UnpackLow(quad3.AsInt64(), quad7.AsInt64()).AsUInt16();
        Vector128<ushort> column7 = Vector128_.UnpackHigh(quad3.AsInt64(), quad7.AsInt64()).AsUInt16();
        column0.StoreUnsafe(ref destinationBase, (nuint)(((x + 0) * destinationStride) + y));
        column1.StoreUnsafe(ref destinationBase, (nuint)(((x + 1) * destinationStride) + y));
        column2.StoreUnsafe(ref destinationBase, (nuint)(((x + 2) * destinationStride) + y));
        column3.StoreUnsafe(ref destinationBase, (nuint)(((x + 3) * destinationStride) + y));
        column4.StoreUnsafe(ref destinationBase, (nuint)(((x + 4) * destinationStride) + y));
        column5.StoreUnsafe(ref destinationBase, (nuint)(((x + 5) * destinationStride) + y));
        column6.StoreUnsafe(ref destinationBase, (nuint)(((x + 6) * destinationStride) + y));
        column7.StoreUnsafe(ref destinationBase, (nuint)(((x + 7) * destinationStride) + y));
    }
}
