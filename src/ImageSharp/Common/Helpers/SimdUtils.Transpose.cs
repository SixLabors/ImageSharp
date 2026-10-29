// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp;

/// <summary>
/// Provides SIMD operations for strided sample matrices.
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

        // A tile spans sixteen rows and columns, the largest supported 32-bit register width.
        // Complete each tile before moving on so widely strided destination columns do not displace the whole matrix from cache.
        int tileSize = Vector512<int>.Count;
        for (int rowBase = 0; rowBase < rows; rowBase += tileSize)
        {
            int rowEnd = Math.Min(rowBase + tileSize, rows);
            for (int columnBase = 0; columnBase < columns; columnBase += tileSize)
            {
                int columnEnd = Math.Min(columnBase + tileSize, columns);
                int row = rowBase;

                // Transpose4 treats each 128-bit group as a separate four-by-four matrix.
                // Wider loads handle adjacent matrices together; the stores place each four-word column at its true destination stride.
                for (; row + 4 <= rowEnd; row += 4)
                {
                    int column = columnBase;
                    if (Vector512.IsHardwareAccelerated)
                    {
                        for (; column + Vector512<int>.Count <= columnEnd; column += Vector512<int>.Count)
                        {
                            Vector512<float> row0 = Vector512.LoadUnsafe(ref input, (nuint)((row * sourceStride) + column)).AsSingle();
                            Vector512<float> row1 = Vector512.LoadUnsafe(ref input, (nuint)(((row + 1) * sourceStride) + column)).AsSingle();
                            Vector512<float> row2 = Vector512.LoadUnsafe(ref input, (nuint)(((row + 2) * sourceStride) + column)).AsSingle();
                            Vector512<float> row3 = Vector512.LoadUnsafe(ref input, (nuint)(((row + 3) * sourceStride) + column)).AsSingle();
                            Transpose4(
                                row0,
                                row1,
                                row2,
                                row3,
                                out Vector512<float> column0,
                                out Vector512<float> column1,
                                out Vector512<float> column2,
                                out Vector512<float> column3);

                            StoreTransposedColumns(column0.AsInt32(), ref output, destinationStride, row, column + 0);
                            StoreTransposedColumns(column1.AsInt32(), ref output, destinationStride, row, column + 1);
                            StoreTransposedColumns(column2.AsInt32(), ref output, destinationStride, row, column + 2);
                            StoreTransposedColumns(column3.AsInt32(), ref output, destinationStride, row, column + 3);
                        }
                    }

                    if (Vector256.IsHardwareAccelerated)
                    {
                        for (; column + Vector256<int>.Count <= columnEnd; column += Vector256<int>.Count)
                        {
                            Vector256<float> row0 = Vector256.LoadUnsafe(ref input, (nuint)((row * sourceStride) + column)).AsSingle();
                            Vector256<float> row1 = Vector256.LoadUnsafe(ref input, (nuint)(((row + 1) * sourceStride) + column)).AsSingle();
                            Vector256<float> row2 = Vector256.LoadUnsafe(ref input, (nuint)(((row + 2) * sourceStride) + column)).AsSingle();
                            Vector256<float> row3 = Vector256.LoadUnsafe(ref input, (nuint)(((row + 3) * sourceStride) + column)).AsSingle();
                            Transpose4(
                                row0,
                                row1,
                                row2,
                                row3,
                                out Vector256<float> column0,
                                out Vector256<float> column1,
                                out Vector256<float> column2,
                                out Vector256<float> column3);

                            StoreTransposedColumns(column0.AsInt32(), ref output, destinationStride, row, column + 0);
                            StoreTransposedColumns(column1.AsInt32(), ref output, destinationStride, row, column + 1);
                            StoreTransposedColumns(column2.AsInt32(), ref output, destinationStride, row, column + 2);
                            StoreTransposedColumns(column3.AsInt32(), ref output, destinationStride, row, column + 3);
                        }
                    }

                    if (Vector128.IsHardwareAccelerated)
                    {
                        for (; column + Vector128<int>.Count <= columnEnd; column += Vector128<int>.Count)
                        {
                            Vector128<float> row0 = Vector128.LoadUnsafe(ref input, (nuint)((row * sourceStride) + column)).AsSingle();
                            Vector128<float> row1 = Vector128.LoadUnsafe(ref input, (nuint)(((row + 1) * sourceStride) + column)).AsSingle();
                            Vector128<float> row2 = Vector128.LoadUnsafe(ref input, (nuint)(((row + 2) * sourceStride) + column)).AsSingle();
                            Vector128<float> row3 = Vector128.LoadUnsafe(ref input, (nuint)(((row + 3) * sourceStride) + column)).AsSingle();
                            Transpose4(
                                row0,
                                row1,
                                row2,
                                row3,
                                out Vector128<float> column0,
                                out Vector128<float> column1,
                                out Vector128<float> column2,
                                out Vector128<float> column3);

                            column0.AsInt32().StoreUnsafe(ref output, (nuint)(((column + 0) * destinationStride) + row));
                            column1.AsInt32().StoreUnsafe(ref output, (nuint)(((column + 1) * destinationStride) + row));
                            column2.AsInt32().StoreUnsafe(ref output, (nuint)(((column + 2) * destinationStride) + row));
                            column3.AsInt32().StoreUnsafe(ref output, (nuint)(((column + 3) * destinationStride) + row));
                        }
                    }

                    // The column remainder has fewer than four elements. Move four independent rows without reading padding.
                    for (; column < columnEnd; column++)
                    {
                        for (int lane = 0; lane < 4; lane++)
                        {
                            Unsafe.Add(ref output, (column * destinationStride) + row + lane) =
                                Unsafe.Add(ref input, ((row + lane) * sourceStride) + column);
                        }
                    }
                }

                // Up to three rows remain at the bottom of this tile. The same sample equation also covers scalar-only hardware.
                for (; row < rowEnd; row++)
                {
                    for (int column = columnBase; column < columnEnd; column++)
                    {
                        Unsafe.Add(ref output, (column * destinationStride) + row) = Unsafe.Add(ref input, (row * sourceStride) + column);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Places two independent four-word columns from a 256-bit transpose at their strided matrix positions.
    /// </summary>
    /// <param name="columns">Four source rows from column c followed by four source rows from column c + 4.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="stride">The destination row stride, in integers.</param>
    /// <param name="row">The first source row, which becomes a destination column.</param>
    /// <param name="column">The first source column, which becomes a destination row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposedColumns(Vector256<int> columns, ref int destination, int stride, int row, int column)
    {
        columns.GetLower().StoreUnsafe(ref destination, (nuint)((column * stride) + row));
        columns.GetUpper().StoreUnsafe(ref destination, (nuint)(((column + 4) * stride) + row));
    }

    /// <summary>
    /// Places four independent four-word columns from a 512-bit transpose at their strided matrix positions.
    /// </summary>
    /// <param name="columns">Four source rows from each of columns c, c + 4, c + 8 and c + 12, in that order.</param>
    /// <param name="destination">The first destination sample.</param>
    /// <param name="stride">The destination row stride, in integers.</param>
    /// <param name="row">The first source row, which becomes a destination column.</param>
    /// <param name="column">The first source column, which becomes a destination row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreTransposedColumns(Vector512<int> columns, ref int destination, int stride, int row, int column)
    {
        columns.GetLower().GetLower().StoreUnsafe(ref destination, (nuint)((column * stride) + row));
        columns.GetLower().GetUpper().StoreUnsafe(ref destination, (nuint)(((column + 4) * stride) + row));
        columns.GetUpper().GetLower().StoreUnsafe(ref destination, (nuint)(((column + 8) * stride) + row));
        columns.GetUpper().GetUpper().StoreUnsafe(ref destination, (nuint)(((column + 12) * stride) + row));
    }
}
