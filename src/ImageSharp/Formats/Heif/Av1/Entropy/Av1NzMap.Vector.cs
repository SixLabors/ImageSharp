// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <content>
/// Derives the nonzero-map contexts of a whole transform block with vector operations.
/// </content>
internal static partial class Av1NzMap
{
    /// <summary>
    /// The two-dimensional positional context offsets as bytes, in the order of <see cref="NzMapContextOffset"/>.
    /// </summary>
    private static readonly byte[][] NzMapContextOffsetBytes = BuildOffsetBytes();

    /// <summary>
    /// The horizontal-class offsets of the first four columns of one row, packed little-endian.
    /// </summary>
    private const uint HorizontalRow4 = NzMapContext0 | (NzMapContext5 << 8) | (NzMapContext10 << 16) | ((uint)NzMapContext10 << 24);

    /// <summary>
    /// The horizontal-class offsets of eight columns beyond the first two, packed little-endian.
    /// </summary>
    private const ulong HorizontalRest8 = NzMapContext10 * 0x0101010101010101UL;

    /// <summary>
    /// The horizontal-class offsets of the first eight columns of one row, packed little-endian.
    /// </summary>
    private const ulong HorizontalRow8 = HorizontalRow4 | (HorizontalRest8 << 32);

    /// <summary>
    /// Gets the two-dimensional positional context offsets of a transform size as bytes.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The row-major offsets at the coded width.</returns>
    public static ReadOnlySpan<byte> GetContextOffsets(Av1TransformSize transformSize)
        => NzMapContextOffsetBytes[(int)transformSize];

    /// <summary>
    /// Combines a neighboring-level statistic with the coefficient's position band, reading the
    /// two-dimensional offsets from a table hoisted by the caller.
    /// </summary>
    /// <param name="stats">The clipped sum of the applicable forward-neighbor magnitudes.</param>
    /// <param name="coefficientIndex">The row-major coefficient index in the coded transform region.</param>
    /// <param name="widthLog2">The base-two logarithm of the coded transform width.</param>
    /// <param name="offsets">The first entry of the two-dimensional offset table of the transform size.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <returns>The nonzero-map probability context.</returns>
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

        int ctx = Math.Min((stats + 1) >> 1, 4);
        switch (transformClass)
        {
            case Av1TransformClass.Class2D:
                return ctx + Unsafe.Add(ref offsets, coefficientIndex);
            case Av1TransformClass.ClassHorizontal:
                return ctx + GetOneDimensionalOffset(coefficientIndex & ((1 << widthLog2) - 1));
            default:
                return ctx + GetOneDimensionalOffset(coefficientIndex >> widthLog2);
        }
    }

    /// <summary>
    /// Gets the one-dimensional positional offset of a row or column index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetOneDimensionalOffset(int index)
        => index == 0 ? NzMapContext0 : index == 1 ? NzMapContext5 : NzMapContext10;

    /// <summary>
    /// Computes the nonzero-map context of every coefficient position of a transform block.
    /// </summary>
    /// <remarks>
    /// This follows <c>av1_get_nz_map_contexts_sse2</c> in the row-major level layout of this port. Each lane sums
    /// the clipped magnitudes of the neighbors selected by the transform class, halves the sum with rounding, clips
    /// it to four, and adds the positional offset of the coefficient. Sixteen positions complete in one vector:
    /// one row of a wide transform, two rows of an eight-wide transform, or four rows of a four-wide transform.
    /// The caller writes the DC and end-of-block contexts afterwards, as <see cref="GetNzMapContextFromStats(int, int, int, Av1TransformSize, Av1TransformClass)"/>
    /// defines them.
    /// </remarks>
    /// <param name="levelBase">The first coded level of the padded plane.</param>
    /// <param name="stride">The padded row stride of the level plane.</param>
    /// <param name="width">The coded transform width.</param>
    /// <param name="height">The coded transform height.</param>
    /// <param name="transformSize">The transform size selecting the positional table.</param>
    /// <param name="transformClass">The transform direction class.</param>
    /// <param name="contextBase">The first entry of the row-major context output.</param>
    public static void GetNzMapContextsVector(
        ref byte levelBase,
        int stride,
        int width,
        int height,
        Av1TransformSize transformSize,
        Av1TransformClass transformClass,
        ref sbyte contextBase)
    {
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

        ref byte tableBase = ref MemoryMarshal.GetReference(table);
        if (width == 4)
        {
            // Four rows share one vector. Every neighbor set is four rows of four bytes at one offset.
            for (int y = 0; y < height; y += 4)
            {
                ref byte row = ref Unsafe.Add(ref levelBase, y * stride);
                Vector128<byte> count = GetContextCounts(
                    Load4x4(ref row, stride, 1),
                    Load4x4(ref row, stride, stride),
                    Load4x4(ref row, stride, offset0),
                    Load4x4(ref row, stride, offset1),
                    Load4x4(ref row, stride, offset2));

                Vector128<byte> positions = transformClass switch
                {
                    Av1TransformClass.Class2D => Vector128.LoadUnsafe(ref tableBase, (nuint)(y * 4)),
                    Av1TransformClass.ClassHorizontal => Vector128.Create(HorizontalRow4).AsByte(),
                    _ => Vector128.Create(
                        Vector128.Create(GetVerticalOffset(y)).AsUInt32().ToScalar(),
                        Vector128.Create(GetVerticalOffset(y + 1)).AsUInt32().ToScalar(),
                        Vector128.Create(GetVerticalOffset(y + 2)).AsUInt32().ToScalar(),
                        Vector128.Create(GetVerticalOffset(y + 3)).AsUInt32().ToScalar()).AsByte(),
                };

                (count + positions).AsSByte().StoreUnsafe(ref contextBase, (nuint)(y * 4));
            }
        }
        else if (width == 8)
        {
            // Two rows share one vector. Every neighbor set is two rows of eight bytes at one offset.
            for (int y = 0; y < height; y += 2)
            {
                ref byte row = ref Unsafe.Add(ref levelBase, y * stride);
                Vector128<byte> count = GetContextCounts(
                    Load8x2(ref row, stride, 1),
                    Load8x2(ref row, stride, stride),
                    Load8x2(ref row, stride, offset0),
                    Load8x2(ref row, stride, offset1),
                    Load8x2(ref row, stride, offset2));

                Vector128<byte> positions = transformClass switch
                {
                    Av1TransformClass.Class2D => Vector128.LoadUnsafe(ref tableBase, (nuint)(y * 8)),
                    Av1TransformClass.ClassHorizontal => Vector128.Create(HorizontalRow8).AsByte(),
                    _ => Vector128.Create(
                        Vector128.Create(GetVerticalOffset(y)).AsUInt64().ToScalar(),
                        Vector128.Create(GetVerticalOffset(y + 1)).AsUInt64().ToScalar()).AsByte(),
                };

                (count + positions).AsSByte().StoreUnsafe(ref contextBase, (nuint)(y * 8));
            }
        }
        else
        {
            Vector128<byte> horizontalFirst = Vector128.Create(HorizontalRow8, HorizontalRest8).AsByte();
            Vector128<byte> horizontalRest = Vector128.Create((byte)NzMapContext10);
            for (int y = 0; y < height; y++)
            {
                ref byte row = ref Unsafe.Add(ref levelBase, y * stride);
                Vector128<byte> vertical = Vector128.Create(GetVerticalOffset(y));
                for (int x = 0; x < width; x += Vector128<byte>.Count)
                {
                    ref byte level = ref Unsafe.Add(ref row, x);
                    Vector128<byte> count = GetContextCounts(
                        Vector128.LoadUnsafe(ref level, 1),
                        Vector128.LoadUnsafe(ref level, (nuint)stride),
                        Vector128.LoadUnsafe(ref level, (nuint)offset0),
                        Vector128.LoadUnsafe(ref level, (nuint)offset1),
                        Vector128.LoadUnsafe(ref level, (nuint)offset2));

                    Vector128<byte> positions = transformClass switch
                    {
                        Av1TransformClass.Class2D => Vector128.LoadUnsafe(ref tableBase, (nuint)((y * width) + x)),
                        Av1TransformClass.ClassHorizontal => x == 0 ? horizontalFirst : horizontalRest,
                        _ => vertical,
                    };

                    (count + positions).AsSByte().StoreUnsafe(ref contextBase, (nuint)((y * width) + x));
                }
            }
        }

        if (transformClass == Av1TransformClass.Class2D)
        {
            contextBase = 0;
        }
    }

    /// <summary>
    /// Sums five clipped neighbor levels per lane, halves with rounding, and clips to the four magnitude bands.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> GetContextCounts(
        Vector128<byte> level0,
        Vector128<byte> level1,
        Vector128<byte> level2,
        Vector128<byte> level3,
        Vector128<byte> level4)
    {
        Vector128<byte> three = Vector128.Create((byte)3);
        Vector128<byte> count = Vector128.Min(level0, three) + Vector128.Min(level1, three) + Vector128.Min(level2, three) +
            Vector128.Min(level3, three) + Vector128.Min(level4, three);

        // (count + 1) >> 1 for every byte: the sum is at most 15, so a 16-bit shift with a lane mask is exact.
        count = ((count + Vector128<byte>.One).AsUInt16() >>> 1).AsByte() & Vector128.Create((byte)0x7F);
        return Vector128.Min(count, Vector128.Create((byte)4));
    }

    /// <summary>
    /// Loads four bytes from each of four consecutive rows into one vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Load4x4(ref byte row, int stride, int offset)
        => Vector128.Create(
            Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, offset)),
            Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, stride + offset)),
            Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, (2 * stride) + offset)),
            Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref row, (3 * stride) + offset))).AsByte();

    /// <summary>
    /// Loads eight bytes from each of two consecutive rows into one vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Load8x2(ref byte row, int stride, int offset)
        => Vector128.Create(
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, offset)),
            Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref row, stride + offset))).AsByte();

    /// <summary>
    /// Gets the one-dimensional positional offset of a row for the vertical transform class.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte GetVerticalOffset(int row)
        => row == 0 ? (byte)NzMapContext0 : row == 1 ? (byte)NzMapContext5 : (byte)NzMapContext10;

    /// <summary>
    /// Converts the positional context tables to bytes for vector loads.
    /// </summary>
    /// <returns>The byte tables in transform-size order.</returns>
    private static byte[][] BuildOffsetBytes()
    {
        byte[][] tables = new byte[NzMapContextOffset.Length][];
        for (int i = 0; i < tables.Length; i++)
        {
            int[] source = NzMapContextOffset[i];
            byte[] bytes = new byte[source.Length];
            for (int j = 0; j < bytes.Length; j++)
            {
                bytes[j] = (byte)source[j];
            }

            tables[i] = bytes;
        }

        return tables;
    }
}
