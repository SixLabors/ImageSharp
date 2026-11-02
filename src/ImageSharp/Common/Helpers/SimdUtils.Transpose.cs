// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides shared matrix transposition for SIMD registers and strided sample buffers.
/// </summary>
internal static partial class SimdUtils
{
    /// <summary>
    /// Transposes the logical rectangle of a floating-point matrix without changing sample bits.
    /// </summary>
    /// <param name="source">The source storage from its logical origin. It must not overlap destination.</param>
    /// <param name="sourceStride">The source row stride, in samples.</param>
    /// <param name="destination">The destination storage from its logical origin.</param>
    /// <param name="destinationStride">The destination row stride, in samples.</param>
    /// <param name="rows">The number of source rows and destination columns.</param>
    /// <param name="columns">The number of source columns and destination rows.</param>
    public static void Transpose(ReadOnlySpan<float> source, int sourceStride, Span<float> destination, int destinationStride, int rows, int columns)
        // The integer operation only moves 32-bit words. It preserves NaN payloads, signed zero and every other floating-point bit.
        => Transpose(MemoryMarshal.Cast<float, int>(source), sourceStride, MemoryMarshal.Cast<float, int>(destination), destinationStride, rows, columns);

    /// <summary>
    /// Transposes a logical integer rectangle with independent source and destination strides.
    /// </summary>
    /// <param name="source">The source storage from its logical origin. It must not overlap destination.</param>
    /// <param name="sourceStride">The source row stride, in integers.</param>
    /// <param name="destination">The destination storage from its logical origin. Padding remains unchanged.</param>
    /// <param name="destinationStride">The destination row stride, in integers.</param>
    /// <param name="rows">The number of source rows and destination columns.</param>
    /// <param name="columns">The number of source columns and destination rows.</param>
    public static void Transpose(ReadOnlySpan<int> source, int sourceStride, Span<int> destination, int destinationStride, int rows, int columns)
    {
        ref int input = ref MemoryMarshal.GetReference(source);
        ref int output = ref MemoryMarshal.GetReference(destination);

        // Complete each sixteen-by-sixteen cache tile before advancing. A complete register kernel exchanges both row and column index bits.
        // Smaller kernels consume incomplete tiles without reading source padding or writing destination padding.
        const int tileSize = 16;

        for (int rowBase = 0; rowBase < rows; rowBase += tileSize)
        {
            int rowEnd = Math.Min(rowBase + tileSize, rows);
            for (int columnBase = 0; columnBase < columns; columnBase += tileSize)
            {
                int columnEnd = Math.Min(columnBase + tileSize, columns);
                int row = rowBase;

                while (row < rowEnd)
                {
                    int rowCount = Vector512.IsHardwareAccelerated && rowEnd - row >= 16 ? 16
                        : Vector256.IsHardwareAccelerated && rowEnd - row >= 8 ? 8
                        : Vector128.IsHardwareAccelerated && rowEnd - row >= 4 ? 4 : 1;

                    int column = columnBase;

                    if (Vector512.IsHardwareAccelerated && rowCount >= 16)
                    {
                        for (; column <= columnEnd - 16; column += 16)
                        {
                            for (int subRow = row; subRow < row + rowCount; subRow += 16)
                            {
                                Vector512<int> row0 = Vector512.LoadUnsafe(ref input, (nuint)((subRow * sourceStride) + column));
                                Vector512<int> row1 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 1) * sourceStride) + column));
                                Vector512<int> row2 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 2) * sourceStride) + column));
                                Vector512<int> row3 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 3) * sourceStride) + column));
                                Vector512<int> row4 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 4) * sourceStride) + column));
                                Vector512<int> row5 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 5) * sourceStride) + column));
                                Vector512<int> row6 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 6) * sourceStride) + column));
                                Vector512<int> row7 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 7) * sourceStride) + column));
                                Vector512<int> row8 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 8) * sourceStride) + column));
                                Vector512<int> row9 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 9) * sourceStride) + column));
                                Vector512<int> row10 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 10) * sourceStride) + column));
                                Vector512<int> row11 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 11) * sourceStride) + column));
                                Vector512<int> row12 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 12) * sourceStride) + column));
                                Vector512<int> row13 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 13) * sourceStride) + column));
                                Vector512<int> row14 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 14) * sourceStride) + column));
                                Vector512<int> row15 = Vector512.LoadUnsafe(ref input, (nuint)(((subRow + 15) * sourceStride) + column));

                                Transpose(
                                    ref row0,
                                    ref row1,
                                    ref row2,
                                    ref row3,
                                    ref row4,
                                    ref row5,
                                    ref row6,
                                    ref row7,
                                    ref row8,
                                    ref row9,
                                    ref row10,
                                    ref row11,
                                    ref row12,
                                    ref row13,
                                    ref row14,
                                    ref row15);

                                row0.StoreUnsafe(ref output, (nuint)((column * destinationStride) + subRow));
                                row1.StoreUnsafe(ref output, (nuint)(((column + 1) * destinationStride) + subRow));
                                row2.StoreUnsafe(ref output, (nuint)(((column + 2) * destinationStride) + subRow));
                                row3.StoreUnsafe(ref output, (nuint)(((column + 3) * destinationStride) + subRow));
                                row4.StoreUnsafe(ref output, (nuint)(((column + 4) * destinationStride) + subRow));
                                row5.StoreUnsafe(ref output, (nuint)(((column + 5) * destinationStride) + subRow));
                                row6.StoreUnsafe(ref output, (nuint)(((column + 6) * destinationStride) + subRow));
                                row7.StoreUnsafe(ref output, (nuint)(((column + 7) * destinationStride) + subRow));
                                row8.StoreUnsafe(ref output, (nuint)(((column + 8) * destinationStride) + subRow));
                                row9.StoreUnsafe(ref output, (nuint)(((column + 9) * destinationStride) + subRow));
                                row10.StoreUnsafe(ref output, (nuint)(((column + 10) * destinationStride) + subRow));
                                row11.StoreUnsafe(ref output, (nuint)(((column + 11) * destinationStride) + subRow));
                                row12.StoreUnsafe(ref output, (nuint)(((column + 12) * destinationStride) + subRow));
                                row13.StoreUnsafe(ref output, (nuint)(((column + 13) * destinationStride) + subRow));
                                row14.StoreUnsafe(ref output, (nuint)(((column + 14) * destinationStride) + subRow));
                                row15.StoreUnsafe(ref output, (nuint)(((column + 15) * destinationStride) + subRow));
                            }
                        }
                    }

                    if (Vector256.IsHardwareAccelerated && rowCount >= 8)
                    {
                        for (; column <= columnEnd - 8; column += 8)
                        {
                            for (int subRow = row; subRow < row + rowCount; subRow += 8)
                            {
                                Vector256<int> row0 = Vector256.LoadUnsafe(ref input, (nuint)((subRow * sourceStride) + column));
                                Vector256<int> row1 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 1) * sourceStride) + column));
                                Vector256<int> row2 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 2) * sourceStride) + column));
                                Vector256<int> row3 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 3) * sourceStride) + column));
                                Vector256<int> row4 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 4) * sourceStride) + column));
                                Vector256<int> row5 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 5) * sourceStride) + column));
                                Vector256<int> row6 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 6) * sourceStride) + column));
                                Vector256<int> row7 = Vector256.LoadUnsafe(ref input, (nuint)(((subRow + 7) * sourceStride) + column));

                                Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
                                row0.StoreUnsafe(ref output, (nuint)((column * destinationStride) + subRow));
                                row1.StoreUnsafe(ref output, (nuint)(((column + 1) * destinationStride) + subRow));
                                row2.StoreUnsafe(ref output, (nuint)(((column + 2) * destinationStride) + subRow));
                                row3.StoreUnsafe(ref output, (nuint)(((column + 3) * destinationStride) + subRow));
                                row4.StoreUnsafe(ref output, (nuint)(((column + 4) * destinationStride) + subRow));
                                row5.StoreUnsafe(ref output, (nuint)(((column + 5) * destinationStride) + subRow));
                                row6.StoreUnsafe(ref output, (nuint)(((column + 6) * destinationStride) + subRow));
                                row7.StoreUnsafe(ref output, (nuint)(((column + 7) * destinationStride) + subRow));
                            }
                        }
                    }

                    if (Vector128.IsHardwareAccelerated && rowCount >= 4)
                    {
                        for (; column <= columnEnd - 4; column += 4)
                        {
                            for (int subRow = row; subRow < row + rowCount; subRow += 4)
                            {
                                Vector128<int> row0 = Vector128.LoadUnsafe(ref input, (nuint)((subRow * sourceStride) + column));
                                Vector128<int> row1 = Vector128.LoadUnsafe(ref input, (nuint)(((subRow + 1) * sourceStride) + column));
                                Vector128<int> row2 = Vector128.LoadUnsafe(ref input, (nuint)(((subRow + 2) * sourceStride) + column));
                                Vector128<int> row3 = Vector128.LoadUnsafe(ref input, (nuint)(((subRow + 3) * sourceStride) + column));

                                Transpose(ref row0, ref row1, ref row2, ref row3);
                                row0.StoreUnsafe(ref output, (nuint)((column * destinationStride) + subRow));
                                row1.StoreUnsafe(ref output, (nuint)(((column + 1) * destinationStride) + subRow));
                                row2.StoreUnsafe(ref output, (nuint)(((column + 2) * destinationStride) + subRow));
                                row3.StoreUnsafe(ref output, (nuint)(((column + 3) * destinationStride) + subRow));
                            }
                        }
                    }

                    // Fewer than four columns remain, or no vector kernel is available. Each assignment moves exactly one logical sample.
                    for (; column < columnEnd; column++)
                    {
                        for (int subRow = row; subRow < row + rowCount; subRow++)
                        {
                            Unsafe.Add(ref output, (column * destinationStride) + subRow) = Unsafe.Add(ref input, (subRow * sourceStride) + column);
                        }
                    }

                    row += rowCount;
                }
            }
        }
    }

    /// <summary>
    /// Transposes a 4-by-4 matrix of 32-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(ref Vector128<int> row0, ref Vector128<int> row1, ref Vector128<int> row2, ref Vector128<int> row3)
    {
        Vector128<int> pairs01Low = Vector128_.UnpackLow(row0, row1);
        Vector128<int> pairs01High = Vector128_.UnpackHigh(row0, row1);
        Vector128<int> pairs23Low = Vector128_.UnpackLow(row2, row3);
        Vector128<int> pairs23High = Vector128_.UnpackHigh(row2, row3);

        row0 = Vector128_.UnpackLow(pairs01Low.AsInt64(), pairs23Low.AsInt64()).AsInt32();
        row1 = Vector128_.UnpackHigh(pairs01Low.AsInt64(), pairs23Low.AsInt64()).AsInt32();
        row2 = Vector128_.UnpackLow(pairs01High.AsInt64(), pairs23High.AsInt64()).AsInt32();
        row3 = Vector128_.UnpackHigh(pairs01High.AsInt64(), pairs23High.AsInt64()).AsInt32();
    }

    /// <summary>
    /// Transposes an 8-by-8 matrix of 32-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    /// <param name="row4">The fifth input row, replaced by the fifth output row.</param>
    /// <param name="row5">The sixth input row, replaced by the sixth output row.</param>
    /// <param name="row6">The seventh input row, replaced by the seventh output row.</param>
    /// <param name="row7">The eighth input row, replaced by the eighth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(
        ref Vector256<int> row0,
        ref Vector256<int> row1,
        ref Vector256<int> row2,
        ref Vector256<int> row3,
        ref Vector256<int> row4,
        ref Vector256<int> row5,
        ref Vector256<int> row6,
        ref Vector256<int> row7)
    {
        Vector256<long> pair0 = Vector256_.UnpackLow(row0, row1).AsInt64();
        Vector256<long> pair1 = Vector256_.UnpackHigh(row0, row1).AsInt64();
        Vector256<long> pair2 = Vector256_.UnpackLow(row2, row3).AsInt64();
        Vector256<long> pair3 = Vector256_.UnpackHigh(row2, row3).AsInt64();
        Vector256<long> pair4 = Vector256_.UnpackLow(row4, row5).AsInt64();
        Vector256<long> pair5 = Vector256_.UnpackHigh(row4, row5).AsInt64();
        Vector256<long> pair6 = Vector256_.UnpackLow(row6, row7).AsInt64();
        Vector256<long> pair7 = Vector256_.UnpackHigh(row6, row7).AsInt64();
        Vector256<int> quad0 = Vector256_.UnpackLow(pair0, pair2).AsInt32();
        Vector256<int> quad1 = Vector256_.UnpackHigh(pair0, pair2).AsInt32();
        Vector256<int> quad2 = Vector256_.UnpackLow(pair1, pair3).AsInt32();
        Vector256<int> quad3 = Vector256_.UnpackHigh(pair1, pair3).AsInt32();
        Vector256<int> quad4 = Vector256_.UnpackLow(pair4, pair6).AsInt32();
        Vector256<int> quad5 = Vector256_.UnpackHigh(pair4, pair6).AsInt32();
        Vector256<int> quad6 = Vector256_.UnpackLow(pair5, pair7).AsInt32();
        Vector256<int> quad7 = Vector256_.UnpackHigh(pair5, pair7).AsInt32();

        // Each 128-bit lane now holds a 4x4 transpose. The exchange of the lanes completes the 8x8 transpose.
        row0 = Vector256.Create(quad0.GetLower(), quad4.GetLower());
        row1 = Vector256.Create(quad1.GetLower(), quad5.GetLower());
        row2 = Vector256.Create(quad2.GetLower(), quad6.GetLower());
        row3 = Vector256.Create(quad3.GetLower(), quad7.GetLower());
        row4 = Vector256.Create(quad0.GetUpper(), quad4.GetUpper());
        row5 = Vector256.Create(quad1.GetUpper(), quad5.GetUpper());
        row6 = Vector256.Create(quad2.GetUpper(), quad6.GetUpper());
        row7 = Vector256.Create(quad3.GetUpper(), quad7.GetUpper());
    }

    /// <summary>
    /// Transposes a 16-by-16 matrix of 32-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    /// <param name="row4">The fifth input row, replaced by the fifth output row.</param>
    /// <param name="row5">The sixth input row, replaced by the sixth output row.</param>
    /// <param name="row6">The seventh input row, replaced by the seventh output row.</param>
    /// <param name="row7">The eighth input row, replaced by the eighth output row.</param>
    /// <param name="row8">The ninth input row, replaced by the ninth output row.</param>
    /// <param name="row9">The tenth input row, replaced by the tenth output row.</param>
    /// <param name="row10">The eleventh input row, replaced by the eleventh output row.</param>
    /// <param name="row11">The twelfth input row, replaced by the twelfth output row.</param>
    /// <param name="row12">The thirteenth input row, replaced by the thirteenth output row.</param>
    /// <param name="row13">The fourteenth input row, replaced by the fourteenth output row.</param>
    /// <param name="row14">The fifteenth input row, replaced by the fifteenth output row.</param>
    /// <param name="row15">The sixteenth input row, replaced by the sixteenth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(
        ref Vector512<int> row0,
        ref Vector512<int> row1,
        ref Vector512<int> row2,
        ref Vector512<int> row3,
        ref Vector512<int> row4,
        ref Vector512<int> row5,
        ref Vector512<int> row6,
        ref Vector512<int> row7,
        ref Vector512<int> row8,
        ref Vector512<int> row9,
        ref Vector512<int> row10,
        ref Vector512<int> row11,
        ref Vector512<int> row12,
        ref Vector512<int> row13,
        ref Vector512<int> row14,
        ref Vector512<int> row15)
    {
        if (Vector512.IsHardwareAccelerated)
        {
            // Each permutation stage exchanges one row-index bit with the matching column-index bit.
            // After four stages, the vector index identifies the source column. The lane index identifies the source row.
            // Keep the complete transpose in 512-bit registers.
            Vector512<int> stage0Lower = Vector512.Create(0, 16, 2, 18, 4, 20, 6, 22, 8, 24, 10, 26, 12, 28, 14, 30);
            Vector512<int> stage0Upper = Vector512.Create(1, 17, 3, 19, 5, 21, 7, 23, 9, 25, 11, 27, 13, 29, 15, 31);
            Vector512<int> lowerSource = row0;
            Vector512<int> upperSource = row1;
            row0 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row1 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row2;
            upperSource = row3;
            row2 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row3 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row4;
            upperSource = row5;
            row4 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row5 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row6;
            upperSource = row7;
            row6 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row7 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row8;
            upperSource = row9;
            row8 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row9 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row10;
            upperSource = row11;
            row10 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row11 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row12;
            upperSource = row13;
            row12 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row13 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row14;
            upperSource = row15;
            row14 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row15 = Vector512_.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);

            Vector512<int> stage1Lower = Vector512.Create(0, 1, 16, 17, 4, 5, 20, 21, 8, 9, 24, 25, 12, 13, 28, 29);
            Vector512<int> stage1Upper = Vector512.Create(2, 3, 18, 19, 6, 7, 22, 23, 10, 11, 26, 27, 14, 15, 30, 31);
            lowerSource = row0;
            upperSource = row2;
            row0 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row2 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row1;
            upperSource = row3;
            row1 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row3 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row4;
            upperSource = row6;
            row4 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row6 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row5;
            upperSource = row7;
            row5 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row7 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row8;
            upperSource = row10;
            row8 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row10 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row9;
            upperSource = row11;
            row9 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row11 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row12;
            upperSource = row14;
            row12 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row14 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row13;
            upperSource = row15;
            row13 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row15 = Vector512_.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);

            Vector512<int> stage2Lower = Vector512.Create(0, 1, 2, 3, 16, 17, 18, 19, 8, 9, 10, 11, 24, 25, 26, 27);
            Vector512<int> stage2Upper = Vector512.Create(4, 5, 6, 7, 20, 21, 22, 23, 12, 13, 14, 15, 28, 29, 30, 31);
            lowerSource = row0;
            upperSource = row4;
            row0 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row4 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row1;
            upperSource = row5;
            row1 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row5 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row2;
            upperSource = row6;
            row2 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row6 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row3;
            upperSource = row7;
            row3 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row7 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row8;
            upperSource = row12;
            row8 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row12 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row9;
            upperSource = row13;
            row9 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row13 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row10;
            upperSource = row14;
            row10 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row14 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row11;
            upperSource = row15;
            row11 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row15 = Vector512_.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);

            Vector512<int> stage3Lower = Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23);
            Vector512<int> stage3Upper = Vector512.Create(8, 9, 10, 11, 12, 13, 14, 15, 24, 25, 26, 27, 28, 29, 30, 31);
            lowerSource = row0;
            upperSource = row8;
            row0 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row8 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row1;
            upperSource = row9;
            row1 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row9 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row2;
            upperSource = row10;
            row2 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row10 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row3;
            upperSource = row11;
            row3 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row11 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row4;
            upperSource = row12;
            row4 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row12 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row5;
            upperSource = row13;
            row5 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row13 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row6;
            upperSource = row14;
            row6 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row14 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row7;
            upperSource = row15;
            row7 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row15 = Vector512_.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            return;
        }

        // The portable path transposes four independent 8-by-8 quadrants with the shared 256-bit kernel.
        // Save the bottom-left quadrant before rows 8 through 15 receive the upper output columns.
        // Publish the upper columns first to reduce the number of live quadrant registers.
        Vector256<int> lowerBottom0 = row8.GetLower();
        Vector256<int> lowerBottom1 = row9.GetLower();
        Vector256<int> lowerBottom2 = row10.GetLower();
        Vector256<int> lowerBottom3 = row11.GetLower();
        Vector256<int> lowerBottom4 = row12.GetLower();
        Vector256<int> lowerBottom5 = row13.GetLower();
        Vector256<int> lowerBottom6 = row14.GetLower();
        Vector256<int> lowerBottom7 = row15.GetLower();
        Vector256<int> upperTop0 = row0.GetUpper();
        Vector256<int> upperTop1 = row1.GetUpper();
        Vector256<int> upperTop2 = row2.GetUpper();
        Vector256<int> upperTop3 = row3.GetUpper();
        Vector256<int> upperTop4 = row4.GetUpper();
        Vector256<int> upperTop5 = row5.GetUpper();
        Vector256<int> upperTop6 = row6.GetUpper();
        Vector256<int> upperTop7 = row7.GetUpper();
        Vector256<int> upperBottom0 = row8.GetUpper();
        Vector256<int> upperBottom1 = row9.GetUpper();
        Vector256<int> upperBottom2 = row10.GetUpper();
        Vector256<int> upperBottom3 = row11.GetUpper();
        Vector256<int> upperBottom4 = row12.GetUpper();
        Vector256<int> upperBottom5 = row13.GetUpper();
        Vector256<int> upperBottom6 = row14.GetUpper();
        Vector256<int> upperBottom7 = row15.GetUpper();

        Transpose(ref upperTop0, ref upperTop1, ref upperTop2, ref upperTop3, ref upperTop4, ref upperTop5, ref upperTop6, ref upperTop7);
        Transpose(
            ref upperBottom0,
            ref upperBottom1,
            ref upperBottom2,
            ref upperBottom3,
            ref upperBottom4,
            ref upperBottom5,
            ref upperBottom6,
            ref upperBottom7);

        row8 = Vector512.Create(upperTop0, upperBottom0);
        row9 = Vector512.Create(upperTop1, upperBottom1);
        row10 = Vector512.Create(upperTop2, upperBottom2);
        row11 = Vector512.Create(upperTop3, upperBottom3);
        row12 = Vector512.Create(upperTop4, upperBottom4);
        row13 = Vector512.Create(upperTop5, upperBottom5);
        row14 = Vector512.Create(upperTop6, upperBottom6);
        row15 = Vector512.Create(upperTop7, upperBottom7);

        Vector256<int> lowerTop0 = row0.GetLower();
        Vector256<int> lowerTop1 = row1.GetLower();
        Vector256<int> lowerTop2 = row2.GetLower();
        Vector256<int> lowerTop3 = row3.GetLower();
        Vector256<int> lowerTop4 = row4.GetLower();
        Vector256<int> lowerTop5 = row5.GetLower();
        Vector256<int> lowerTop6 = row6.GetLower();
        Vector256<int> lowerTop7 = row7.GetLower();

        Transpose(ref lowerTop0, ref lowerTop1, ref lowerTop2, ref lowerTop3, ref lowerTop4, ref lowerTop5, ref lowerTop6, ref lowerTop7);
        Transpose(
            ref lowerBottom0,
            ref lowerBottom1,
            ref lowerBottom2,
            ref lowerBottom3,
            ref lowerBottom4,
            ref lowerBottom5,
            ref lowerBottom6,
            ref lowerBottom7);

        row0 = Vector512.Create(lowerTop0, lowerBottom0);
        row1 = Vector512.Create(lowerTop1, lowerBottom1);
        row2 = Vector512.Create(lowerTop2, lowerBottom2);
        row3 = Vector512.Create(lowerTop3, lowerBottom3);
        row4 = Vector512.Create(lowerTop4, lowerBottom4);
        row5 = Vector512.Create(lowerTop5, lowerBottom5);
        row6 = Vector512.Create(lowerTop6, lowerBottom6);
        row7 = Vector512.Create(lowerTop7, lowerBottom7);
    }

    /// <summary>
    /// Transposes four four-lane rows into four four-lane columns.
    /// </summary>
    /// <param name="row0">The first matrix row.</param>
    /// <param name="row1">The second matrix row.</param>
    /// <param name="row2">The third matrix row.</param>
    /// <param name="row3">The fourth matrix row.</param>
    /// <param name="column0">The first matrix column.</param>
    /// <param name="column1">The second matrix column.</param>
    /// <param name="column2">The third matrix column.</param>
    /// <param name="column3">The fourth matrix column.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector128<float> row0,
        Vector128<float> row1,
        Vector128<float> row2,
        Vector128<float> row3,
        out Vector128<float> column0,
        out Vector128<float> column1,
        out Vector128<float> column2,
        out Vector128<float> column3)
    {
        // The integer core only moves words. Reinterpret the result so NaN payloads and signed zero stay unchanged.
        Vector128<int> a = row0.AsInt32();
        Vector128<int> b = row1.AsInt32();
        Vector128<int> c = row2.AsInt32();
        Vector128<int> d = row3.AsInt32();
        Transpose(ref a, ref b, ref c, ref d);
        column0 = a.AsSingle();
        column1 = b.AsSingle();
        column2 = c.AsSingle();
        column3 = d.AsSingle();
    }

    /// <summary>
    /// Transposes four-by-four matrices independently within each 128-bit group.
    /// </summary>
    /// <param name="row0">The first rows of the matrices.</param>
    /// <param name="row1">The second rows of the matrices.</param>
    /// <param name="row2">The third rows of the matrices.</param>
    /// <param name="row3">The fourth rows of the matrices.</param>
    /// <param name="column0">The first columns of the matrices.</param>
    /// <param name="column1">The second columns of the matrices.</param>
    /// <param name="column2">The third columns of the matrices.</param>
    /// <param name="column3">The fourth columns of the matrices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector256<float> row0,
        Vector256<float> row1,
        Vector256<float> row2,
        Vector256<float> row3,
        out Vector256<float> column0,
        out Vector256<float> column1,
        out Vector256<float> column2,
        out Vector256<float> column3)
    {
        // Match the 128-bit transpose: first pair adjacent 32-bit words, then pair the resulting 64-bit groups.
        // Unpack keeps 128-bit groups separate. Callers permute those groups when they need consecutive color indices.
        Vector256<int> row01Low = Vector256_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector256<int> row01High = Vector256_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector256<int> row23Low = Vector256_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector256<int> row23High = Vector256_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector256_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector256_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector256_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector256_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }

    /// <summary>
    /// Transposes four-by-four matrices independently within each 128-bit group.
    /// </summary>
    /// <param name="row0">The first rows of the matrices.</param>
    /// <param name="row1">The second rows of the matrices.</param>
    /// <param name="row2">The third rows of the matrices.</param>
    /// <param name="row3">The fourth rows of the matrices.</param>
    /// <param name="column0">The first columns of the matrices.</param>
    /// <param name="column1">The second columns of the matrices.</param>
    /// <param name="column2">The third columns of the matrices.</param>
    /// <param name="column3">The fourth columns of the matrices.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4(
        Vector512<float> row0,
        Vector512<float> row1,
        Vector512<float> row2,
        Vector512<float> row3,
        out Vector512<float> column0,
        out Vector512<float> column1,
        out Vector512<float> column2,
        out Vector512<float> column3)
    {
        // Match the 128-bit transpose: first pair adjacent 32-bit words, then pair the resulting 64-bit groups.
        // Unpack keeps 128-bit groups separate. Callers permute those groups when they need consecutive color indices.
        Vector512<int> row01Low = Vector512_.UnpackLow(row0.AsInt32(), row1.AsInt32());
        Vector512<int> row01High = Vector512_.UnpackHigh(row0.AsInt32(), row1.AsInt32());
        Vector512<int> row23Low = Vector512_.UnpackLow(row2.AsInt32(), row3.AsInt32());
        Vector512<int> row23High = Vector512_.UnpackHigh(row2.AsInt32(), row3.AsInt32());

        column0 = Vector512_.UnpackLow(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column1 = Vector512_.UnpackHigh(row01Low.AsInt64(), row23Low.AsInt64()).AsSingle();
        column2 = Vector512_.UnpackLow(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
        column3 = Vector512_.UnpackHigh(row01High.AsInt64(), row23High.AsInt64()).AsSingle();
    }
}
