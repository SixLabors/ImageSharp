// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Derives the nonzero-map context of every coefficient of a transform block.
/// </content>
internal static partial class Av1NzMap
{
    /// <summary>
    /// Gets the one-dimensional positional offset of every index up to the largest extent, the values of
    /// <see cref="GetOneDimensionalOffsetByte"/>, so that a row of them is one load from static data.
    /// </summary>
    private static ReadOnlySpan<byte> OneDimensionalOffsets =>
    [
        NzMapContext0, NzMapContext5, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10,
        NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10, NzMapContext10
    ];

    /// <summary>
    /// Gets the two-dimensional positional offsets of a transform size as bytes.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The row-major offsets at the coded width.</returns>
    public static ReadOnlySpan<byte> GetContextOffsets(Av1TransformSize transformSize)
        => NzMapContextOffsetBytes[(int)transformSize];

    /// <summary>
    /// Derives the nonzero-map context of every coefficient position of a transform block.
    /// </summary>
    /// <param name="levelBase">The first coded level of the padded plane.</param>
    /// <param name="stride">The padded row stride of the level plane.</param>
    /// <param name="width">The coded transform width.</param>
    /// <param name="height">The coded transform height.</param>
    /// <param name="transformSize">The transform size that selects the positional table.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="contextBase">The first entry of the row-major context output.</param>
    /// <remarks>
    /// The caller writes the end-of-block context afterwards, as
    /// <see cref="GetNzMapContextFromStats(int, int, int, Av1TransformSize, Av1TransformClass)"/> defines it.
    /// </remarks>
    public static void GetNzMapContexts(
        ref byte levelBase,
        int stride,
        int width,
        int height,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass,
        ref sbyte contextBase)
        => Contexts<CountOperator>.Apply(ref levelBase, stride, width, height, transformSize, transformClass, ref contextBase);

    /// <summary>
    /// Combines a neighboring-level statistic with the position band of one coefficient, reading
    /// the two-dimensional offsets from a table that the caller hoisted.
    /// </summary>
    /// <param name="stats">The clipped sum of the applicable forward-neighbor magnitudes.</param>
    /// <param name="coefficientIndex">The row-major index in the coded transform region.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="offsets">The first entry of the two-dimensional table of the transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The nonzero-map probability context.</returns>
    /// <remarks>
    /// The DC coefficient of a two-dimensional transform always uses context zero. The combined test of the class and the index finds that case.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetNzMapContextFromStats(
        int stats,
        int coefficientIndex,
        int widthLog2,
        ref byte offsets,
        Av1TransformClass transformClass)
    {
        if (((int)transformClass | coefficientIndex) == 0)
        {
            return 0;
        }

        int band = Math.Min((stats + 1) >> 1, 4);
        return transformClass switch
        {
            Av1TransformClass.Class2D => band + Unsafe.Add(ref offsets, (nuint)(uint)coefficientIndex),
            Av1TransformClass.ClassHorizontal => band + GetOneDimensionalOffsetByte(coefficientIndex & ((1 << widthLog2) - 1)),
            _ => band + GetOneDimensionalOffsetByte(coefficientIndex >> widthLog2),
        };
    }

    /// <summary>
    /// Gets the one-dimensional positional offset of a row or column index.
    /// </summary>
    /// <param name="index">The row index or the column index.</param>
    /// <returns>The positional offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetOneDimensionalOffsetByte(int index)
        => index == 0 ? (byte)NzMapContext0 : index == 1 ? (byte)NzMapContext5 : (byte)NzMapContext10;

    /// <summary>
    /// Traverses a transform block with the arithmetic of a closed reduction operator.
    /// </summary>
    /// <typeparam name="TOperator">The neighbor reduction.</typeparam>
    /// <remarks>
    /// <para>
    /// A context is a magnitude band plus a positional offset. Both values belong to one coefficient, so one lane is one coefficient
    /// throughout. The transform class chooses five fixed sample offsets for the neighbors of a coefficient. As a result, every
    /// neighbor is one shifted load of the level plane.
    /// </para>
    /// <para>
    /// A coded row is a contiguous run in the padded level plane. A whole row of a transform sixteen samples wide or wider fills at
    /// least one 128-bit vector. Those widths walk one row at a time, widest register first. A transform four or eight samples wide
    /// cannot fill a vector from one row, so these widths pack four rows or two rows into one 128-bit vector. They do not pack eight or
    /// sixteen rows. The whole block has at most 256 coefficients, and the extra packing costs more code than the saved iterations give.
    /// </para>
    /// </remarks>
    private static class Contexts<TOperator>
        where TOperator : struct, IAv1NzMapCountOperator
    {
        /// <summary>
        /// Derives the context of every coefficient of one transform block.
        /// </summary>
        /// <param name="levelBase">The first coded level of the padded plane.</param>
        /// <param name="stride">The padded row stride of the level plane.</param>
        /// <param name="width">The coded transform width.</param>
        /// <param name="height">The coded transform height.</param>
        /// <param name="transformSize">The transform size that selects the positional table.</param>
        /// <param name="transformClass">The transform direction class.</param>
        /// <param name="contextBase">The first entry of the row-major context output.</param>
        public static void Apply(
            ref byte levelBase,
            int stride,
            int width,
            int height,
            Av1TransformSize transformSize,
            Av1TransformClass transformClass,
            ref sbyte contextBase)
        {
            // The first two neighbors are always the sample to the right and the sample below. The
            // remaining three are what separates the three transform classes.
            int offset0;
            int offset1;
            int offset2;
            switch (transformClass)
            {
                case Av1TransformClass.Class2D:
                    offset0 = stride + 1;
                    offset1 = 2;
                    offset2 = 2 * stride;
                    break;
                case Av1TransformClass.ClassHorizontal:
                    offset0 = 2;
                    offset1 = 3;
                    offset2 = 4;
                    break;
                default:
                    offset0 = 2 * stride;
                    offset1 = 3 * stride;
                    offset2 = 4 * stride;
                    break;
            }

            ReadOnlySpan<byte> table = transformClass == Av1TransformClass.Class2D
                ? NzMapContextOffsetBytes[(int)transformSize]
                : default;

            if (Vector128.IsHardwareAccelerated)
            {
                if (width >= Vector128<byte>.Count)
                {
                    ApplyRows(ref levelBase, stride, width, height, table, transformClass, offset0, offset1, offset2, ref contextBase);
                }
                else
                {
                    ApplyPacked(ref levelBase, stride, width, height, table, transformClass, offset0, offset1, offset2, ref contextBase);
                }
            }
            else
            {
                ApplyScalar(ref levelBase, stride, width, height, table, transformClass, offset0, offset1, offset2, ref contextBase);
            }

            // The DC coefficient of a two-dimensional transform always uses context zero. The positional table does not encode that case.
            if (transformClass == Av1TransformClass.Class2D)
            {
                contextBase = 0;
            }
        }

        /// <summary>
        /// Derives the contexts of a transform at least sixteen samples wide, one row at a time.
        /// </summary>
        /// <param name="levelBase">The first coded level of the padded plane.</param>
        /// <param name="stride">The padded row stride of the level plane.</param>
        /// <param name="width">The coded transform width.</param>
        /// <param name="height">The coded transform height.</param>
        /// <param name="table">The two-dimensional positional offsets, or empty.</param>
        /// <param name="transformClass">The transform direction class.</param>
        /// <param name="offset0">The sample offset of the third neighbor.</param>
        /// <param name="offset1">The sample offset of the fourth neighbor.</param>
        /// <param name="offset2">The sample offset of the fifth neighbor.</param>
        /// <param name="contextBase">The first entry of the row-major context output.</param>
        private static void ApplyRows(
            ref byte levelBase,
            int stride,
            int width,
            int height,
            ReadOnlySpan<byte> table,
            Av1TransformClass transformClass,
            int offset0,
            int offset1,
            int offset2,
            ref sbyte contextBase)
        {
            ref byte tableBase = ref MemoryMarshal.GetReference(table);

            // The horizontal class gives every row the same offsets, which the static row of offsets holds.
            ref byte horizontalBase = ref MemoryMarshal.GetReference(OneDimensionalOffsets);

            for (int row = 0; row < height; row++)
            {
                ref byte level = ref Unsafe.Add(ref levelBase, row * stride);
                ref sbyte destination = ref Unsafe.Add(ref contextBase, row * width);

                // The vertical class gives every coefficient of a row the same offset.
                byte vertical = GetOneDimensionalOffsetByte(row);
                int column = 0;

                if (Vector512.IsHardwareAccelerated)
                {
                    int vectorEnd = width - Vector512<byte>.Count;
                    for (; column <= vectorEnd; column += Vector512<byte>.Count)
                    {
                        Vector512<byte> count = TOperator.Count(
                            Vector512.LoadUnsafe(ref level, (nuint)(column + 1)),
                            Vector512.LoadUnsafe(ref level, (nuint)(column + stride)),
                            Vector512.LoadUnsafe(ref level, (nuint)(column + offset0)),
                            Vector512.LoadUnsafe(ref level, (nuint)(column + offset1)),
                            Vector512.LoadUnsafe(ref level, (nuint)(column + offset2)));

                        Vector512<byte> positions = transformClass switch
                        {
                            Av1TransformClass.Class2D => Vector512.LoadUnsafe(ref tableBase, (nuint)((row * width) + column)),
                            Av1TransformClass.ClassHorizontal => Vector512.LoadUnsafe(ref horizontalBase, (nuint)column),
                            _ => Vector512.Create(vertical),
                        };

                        (count + positions).AsSByte().StoreUnsafe(ref destination, (nuint)column);
                    }
                }

                if (Vector256.IsHardwareAccelerated)
                {
                    int vectorEnd = width - Vector256<byte>.Count;
                    for (; column <= vectorEnd; column += Vector256<byte>.Count)
                    {
                        Vector256<byte> count = TOperator.Count(
                            Vector256.LoadUnsafe(ref level, (nuint)(column + 1)),
                            Vector256.LoadUnsafe(ref level, (nuint)(column + stride)),
                            Vector256.LoadUnsafe(ref level, (nuint)(column + offset0)),
                            Vector256.LoadUnsafe(ref level, (nuint)(column + offset1)),
                            Vector256.LoadUnsafe(ref level, (nuint)(column + offset2)));

                        Vector256<byte> positions = transformClass switch
                        {
                            Av1TransformClass.Class2D => Vector256.LoadUnsafe(ref tableBase, (nuint)((row * width) + column)),
                            Av1TransformClass.ClassHorizontal => Vector256.LoadUnsafe(ref horizontalBase, (nuint)column),
                            _ => Vector256.Create(vertical),
                        };

                        (count + positions).AsSByte().StoreUnsafe(ref destination, (nuint)column);
                    }
                }

                int lastVector = width - Vector128<byte>.Count;
                for (; column <= lastVector; column += Vector128<byte>.Count)
                {
                    Vector128<byte> count = TOperator.Count(
                        Vector128.LoadUnsafe(ref level, (nuint)(column + 1)),
                        Vector128.LoadUnsafe(ref level, (nuint)(column + stride)),
                        Vector128.LoadUnsafe(ref level, (nuint)(column + offset0)),
                        Vector128.LoadUnsafe(ref level, (nuint)(column + offset1)),
                        Vector128.LoadUnsafe(ref level, (nuint)(column + offset2)));

                    Vector128<byte> positions = transformClass switch
                    {
                        Av1TransformClass.Class2D => Vector128.LoadUnsafe(ref tableBase, (nuint)((row * width) + column)),
                        Av1TransformClass.ClassHorizontal => Vector128.LoadUnsafe(ref horizontalBase, (nuint)column),
                        _ => Vector128.Create(vertical),
                    };

                    (count + positions).AsSByte().StoreUnsafe(ref destination, (nuint)column);
                }
            }
        }

        /// <summary>
        /// Derives the contexts of a transform four or eight samples wide, packing whole rows.
        /// </summary>
        /// <param name="levelBase">The first coded level of the padded plane.</param>
        /// <param name="stride">The padded row stride of the level plane.</param>
        /// <param name="width">The coded transform width, which is four or eight.</param>
        /// <param name="height">The coded transform height.</param>
        /// <param name="table">The two-dimensional positional offsets, or empty.</param>
        /// <param name="transformClass">The transform direction class.</param>
        /// <param name="offset0">The sample offset of the third neighbor.</param>
        /// <param name="offset1">The sample offset of the fourth neighbor.</param>
        /// <param name="offset2">The sample offset of the fifth neighbor.</param>
        /// <param name="contextBase">The first entry of the row-major context output.</param>
        /// <remarks>
        /// Four rows of four samples or two rows of eight samples fill one 128-bit vector. A row is
        /// gathered by its own width, as one four-byte or eight-byte read, because the padded stride
        /// separates the rows in the level plane. The contexts are written back contiguously,
        /// because the output has no padding.
        /// </remarks>
        private static void ApplyPacked(
            ref byte levelBase,
            int stride,
            int width,
            int height,
            ReadOnlySpan<byte> table,
            Av1TransformClass transformClass,
            int offset0,
            int offset1,
            int offset2,
            ref sbyte contextBase)
        {
            ref byte tableBase = ref MemoryMarshal.GetReference(table);
            int rows = Vector128<byte>.Count / width;

            // The one-dimensional offsets of a group depend only on the column, for the horizontal class, or on the
            // row, for the vertical class. Every row from the third on has the same offset, so only the first group of
            // a vertical block differs from the rest. Both patterns are built once, before the loop.
            Span<byte> positionBytes = stackalloc byte[Vector128<byte>.Count];
            for (int lane = 0; lane < Vector128<byte>.Count; lane++)
            {
                positionBytes[lane] = transformClass == Av1TransformClass.ClassHorizontal
                    ? GetOneDimensionalOffsetByte(lane % width)
                    : GetOneDimensionalOffsetByte(lane / width);
            }

            Vector128<byte> firstPositions = Vector128.Create((ReadOnlySpan<byte>)positionBytes);
            Vector128<byte> laterPositions = transformClass == Av1TransformClass.ClassHorizontal
                ? firstPositions
                : Vector128.Create((byte)NzMapContext10);

            for (int row = 0; row < height; row += rows)
            {
                ref byte level = ref Unsafe.Add(ref levelBase, row * stride);
                Vector128<byte> count = TOperator.Count(
                    Pack(ref level, stride, width, rows, 1),
                    Pack(ref level, stride, width, rows, stride),
                    Pack(ref level, stride, width, rows, offset0),
                    Pack(ref level, stride, width, rows, offset1),
                    Pack(ref level, stride, width, rows, offset2));

                Vector128<byte> positions;
                if (transformClass == Av1TransformClass.Class2D)
                {
                    // The table is row-major at the coded width and the output is too, so the
                    // offsets of the packed rows are one contiguous load.
                    positions = Vector128.LoadUnsafe(ref tableBase, (nuint)(row * width));
                }
                else
                {
                    positions = row == 0 ? firstPositions : laterPositions;
                }

                (count + positions).AsSByte()
                    .StoreUnsafe(ref Unsafe.Add(ref contextBase, row * width), 0);
            }
        }

        /// <summary>
        /// Packs one neighbor of several consecutive rows into one vector.
        /// </summary>
        /// <param name="row">The first coded level of the first row of the group.</param>
        /// <param name="stride">The padded row stride of the level plane.</param>
        /// <param name="width">The coded transform width, which is four or eight.</param>
        /// <param name="rows">The number of rows in the group.</param>
        /// <param name="offset">The sample offset of the neighbor.</param>
        /// <returns>The packed levels, with the first row in the lowest lanes.</returns>
        /// <remarks>
        /// The rows are separated by the padded stride, so they cannot be read as one vector. Each
        /// row is read by its own width instead, and the reads are assembled in row order.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Pack(ref byte row, int stride, int width, int rows, int offset)
        {
            if (width == 4)
            {
                return Vector128.Create(
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, offset)),
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, stride + offset)),
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, (2 * stride) + offset)),
                    Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, (3 * stride) + offset))).AsByte();
            }

            return Vector128.Create(
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, offset)),
                Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, stride + offset))).AsByte();
        }

        /// <summary>
        /// Derives the contexts of a transform block one coefficient at a time.
        /// </summary>
        /// <param name="levelBase">The first coded level of the padded plane.</param>
        /// <param name="stride">The padded row stride of the level plane.</param>
        /// <param name="width">The coded transform width.</param>
        /// <param name="height">The coded transform height.</param>
        /// <param name="table">The two-dimensional positional offsets, or empty.</param>
        /// <param name="transformClass">The transform direction class.</param>
        /// <param name="offset0">The sample offset of the third neighbor.</param>
        /// <param name="offset1">The sample offset of the fourth neighbor.</param>
        /// <param name="offset2">The sample offset of the fifth neighbor.</param>
        /// <param name="contextBase">The first entry of the row-major context output.</param>
        /// <remarks>
        /// An unaccelerated runtime reaches the same contexts through the scalar overload of the
        /// same operator, so the caller needs no second derivation of its own.
        /// </remarks>
        private static void ApplyScalar(
            ref byte levelBase,
            int stride,
            int width,
            int height,
            ReadOnlySpan<byte> table,
            Av1TransformClass transformClass,
            int offset0,
            int offset1,
            int offset2,
            ref sbyte contextBase)
        {
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    ref byte level = ref Unsafe.Add(ref levelBase, (row * stride) + column);
                    byte count = TOperator.Count(
                        Unsafe.Add(ref level, 1),
                        Unsafe.Add(ref level, stride),
                        Unsafe.Add(ref level, offset0),
                        Unsafe.Add(ref level, offset1),
                        Unsafe.Add(ref level, offset2));

                    byte position = transformClass switch
                    {
                        Av1TransformClass.Class2D => table[(row * width) + column],
                        Av1TransformClass.ClassHorizontal => GetOneDimensionalOffsetByte(column),
                        _ => GetOneDimensionalOffsetByte(row),
                    };

                    Unsafe.Add(ref contextBase, (row * width) + column) = (sbyte)(count + position);
                }
            }
        }
    }
}
