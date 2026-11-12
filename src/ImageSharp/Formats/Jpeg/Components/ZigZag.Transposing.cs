// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the transposing zig-zag reorder of a quantized block.
/// </content>
internal static partial class ZigZag
{
#pragma warning disable SA1309 // naming rules violation warnings
    /// <summary>
    /// The byte shuffle index that sets the destination byte to zero.
    /// </summary>
    private const byte _ = 0xff;
#pragma warning restore SA1309

    /// <summary>
    /// Gets the byte shuffle masks of the <see cref="Vector128{T}"/> form of <see cref="TransposingZigZagOperator"/>.
    /// </summary>
    /// <remarks>
    /// Each 16-byte mask moves the 16-bit values of one source row to their places in one output row.
    /// The index <c>_</c> sets the destination byte to zero. The output row is the OR of the shuffled source rows.
    /// </remarks>
    private static ReadOnlySpan<byte> SseShuffleMasks =>
    [
#pragma warning disable SA1515
        /* row0 - A0 B0 A1 A2 B1 C0 D0 C1 */
        // A
        0, 1, _, _, 2, 3, 4, 5, _, _, _, _, _, _, _, _,
        // B
        _, _, 0, 1, _, _, _, _, 2, 3, _, _, _, _, _, _,
        // C
        _, _, _, _, _, _, _, _, _, _, 0, 1, _, _, 2, 3,

        /* row1 - B2 A3 A4 B3 C2 D1 E0 F0 */
        // A
        _, _, 6, 7, 8, 9, _, _, _, _, _, _, _, _, _, _,
        // B
        4, 5, _, _, _, _, 6, 7, _, _, _, _, _, _, _, _,

        /* row2 - E1 D2 C3 B4 A5 A6 B5 C4 */
        // A
        _, _, _, _, _, _, _, _, 10, 11, 12, 13,  _,  _, _, _,
        // B
        _, _, _, _, _, _, 8, 9,  _,  _,  _,  _, 10, 11, _, _,
        // C
        _, _, _, _, 6, 7, _, _,  _,  _,  _,  _,  _,  _, 8, 9,

        /* row3 - D3 E2 F1 G0 H0 G1 F2 E3 */
        // E
        _, _, 4, 5, _, _, _, _, _, _, _, _, _, _, 6, 7,
        // F
        _, _, _, _, 2, 3, _, _, _, _, _, _, 4, 5, _, _,
        // G
        _, _, _, _, _, _, 0, 1, _, _, 2, 3, _, _, _, _,

        /* row4 - D4 C5 B6 A7 B7 C6 D5 E4 */
        // B
        _, _,  _,  _, 12, 13, _, _, 14, 15,  _,  _,  _,  _, _, _,
        // C
        _, _, 10, 11,  _,  _, _, _,  _,  _, 12, 13,  _,  _, _, _,
        // D
        8, 9,  _,  _,  _,  _, _, _,  _,  _,  _,  _, 10, 11, _, _,

        /* row5 - F3 G2 H1 H2 G3 F4 E5 D6 */
        // F
        6, 7, _, _, _, _, _, _, _, _, 8, 9, _, _, _, _,
        // G
        _, _, 4, 5, _, _, _, _, 6, 7, _, _, _, _, _, _,
        // H
        _, _, _, _, 2, 3, 4, 5, _, _, _, _, _, _, _, _,

        /* row6 - C7 D7 E6 F5 G4 H3 H4 G5 */
        // G
        _, _, _, _, _, _, _, _, 8, 9, _, _, _, _, 10, 11,
        // H
        _, _, _, _, _, _, _, _, _, _, 6, 7, 8, 9,  _,  _,

        /* row7 - F6 E7 F7 G6 H5 H6 G7 H7 */
        // F
        12, 13, _, _, 14, 15,  _,  _,  _,  _,  _,  _,  _,  _, _, _,
        // G
        _,  _, _, _,  _,  _, 12, 13,  _,  _,  _,  _, 14, 15, _, _,
        // H
        _,  _, _, _,  _,  _,  _,  _, 10, 11, 12, 13,  _,  _, 14, 15,
#pragma warning restore SA1515
    ];

    /// <summary>
    /// Gets the masks of the <see cref="Vector256{T}"/> form of <see cref="TransposingZigZagOperator"/>.
    /// </summary>
    /// <remarks>
    /// The <c>[cr]</c> masks are 32-bit element permutes: they move each needed pair of values into the 128-bit half
    /// of its output row. The <c>(in)</c> masks are byte shuffles inside each 128-bit half. The index <c>_</c> sets
    /// the destination byte to zero. Each output register is the OR of the shuffled source registers.
    /// </remarks>
    private static ReadOnlySpan<byte> AvxShuffleMasks =>
    [
#pragma warning disable SA1515
        /* 01 */
        // [cr] crln_01_AB_CD
        0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   0, 0, 0, 0,   1, 0, 0, 0,   2, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) AB
        0, 1, 8, 9,   2, 3, 4, 5,   10, 11, _, _,   _, _, _, _,   12, 13, 2, 3,   4, 5, 14, 15,   _, _, _, _,   _, _, _, _,
        // (in) CD
        _, _, _, _,   _, _, _, _,   _, _, 0, 1,   8, 9, 2, 3,   _, _, _, _,   _, _, _, _,   0, 1, 10, 11,   _, _, _, _,
        // [cr] crln_01_23_EF_23_CD
        0, 0, 0, 0,   1, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) EF
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   0, 1, 8, 9,

        /* 23 */
        // [cr] crln_23_AB_23_45_GH
        2, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,
        // (in) AB
        _, _, _, _,   _, _, 8, 9,   2, 3, 4, 5,   10, 11, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // (in) CD
        _, _, 12, 13,   6, 7, _, _,   _, _, _, _,   _, _, 8, 9,   14, 15, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // (in) EF
        2, 3, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 4, 5,   10, 11, _, _,   _, _, _, _,   12, 13, 6, 7,
        // (in) GH
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 0, 1,   8, 9, 2, 3,   _, _, _, _,

        /* 45 */
        // (in) AB
        _, _, _, _,   12, 13, 6, 7,   14, 15, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // [cr] crln_45_67_CD_45_EF
        2, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,
        // (in) CD
        8, 9, 2, 3,   _, _, _, _,   _, _, 4, 5,   10, 11, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 12, 13,
        // (in) EF
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 0, 1,   6, 7, _, _,   _, _, _, _,   _, _, 8, 9,   2, 3, _, _,
        // (in) GH
        _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 4, 5,   10, 11, 12, 13,   6, 7, _, _,   _, _, _, _,

        /* 67 */
        // (in) CD
        6, 7, 14, 15,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,
        // [cr] crln_67_EF_67_GH
        2, 0, 0, 0,   3, 0, 0, 0,   5, 0, 0, 0,   6, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   0, 0, 0, 0,
        // (in) EF
        _, _, _, _,   4, 5, 14, 15,   _, _, _, _,   _, _, _, _,   8, 9, 2, 3,   10, 11, _, _,   _, _, _, _,   _, _, _, _,
        // (in) GH
        _, _, _, _,   _, _, _, _,   0, 1, 10, 11,   12, 13, 2, 3,   _, _, _, _,   _, _, 0, 1,   6, 7, 8, 9,   2, 3, 10, 11,
#pragma warning restore SA1515
    ];

    /// <summary>
    /// Reorders a block from transposed order to zig-zag order.
    /// </summary>
    /// <remarks>
    /// Output value <c>i</c> is input value <see cref="TransposingOrder"/>[<c>i</c>]. The encoder stage gives its
    /// coefficients in transposed order. Thus, this reorder gives the standard zig-zag coefficient order.
    /// In the comments, the letters A to H name the input rows 0 to 7, and the digit is the column.
    /// </remarks>
    internal readonly struct TransposingZigZagOperator : Block8x8.IBlockOperator
    {
        /// <inheritdoc/>
        public static void Invoke(ref Block8x8 block)
        {
            Block8x8 source = block;
            ReadOnlySpan<byte> order = TransposingOrder;
            for (int i = 0; i < Block8x8.Size; i++)
            {
                block[i] = source[order[i]];
            }
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector128<short> v0,
            ref Vector128<short> v1,
            ref Vector128<short> v2,
            ref Vector128<short> v3,
            ref Vector128<short> v4,
            ref Vector128<short> v5,
            ref Vector128<short> v6,
            ref Vector128<short> v7)
        {
            // Each output row is the OR of up to three byte-shuffled input rows.
            // The masks place the values that come from those rows. WithElement inserts the other values one at a time.
            ref byte masks = ref MemoryMarshal.GetReference(SseShuffleMasks);
            Vector128<byte> rowA = v0.AsByte();
            Vector128<byte> rowB = v1.AsByte();
            Vector128<byte> rowC = v2.AsByte();
            Vector128<byte> rowD = v3.AsByte();
            Vector128<byte> rowE = v4.AsByte();
            Vector128<byte> rowF = v5.AsByte();
            Vector128<byte> rowG = v6.AsByte();
            Vector128<byte> rowH = v7.AsByte();

            // row0 - A0 B0 A1 A2 B1 C0 D0 C1
            Vector128<short> row0 = Shuffle(rowA, ref masks, 0) | Shuffle(rowB, ref masks, 1) | Shuffle(rowC, ref masks, 2);
            row0 = row0.WithElement(6, rowD.AsInt16().GetElement(0));

            // row1 - B2 A3 A4 B3 C2 D1 E0 F0
            Vector128<short> row1 = Shuffle(rowA, ref masks, 3) | Shuffle(rowB, ref masks, 4);
            row1 = row1.WithElement(4, rowC.AsInt16().GetElement(2));
            row1 = row1.WithElement(5, rowD.AsInt16().GetElement(1));
            row1 = row1.WithElement(6, rowE.AsInt16().GetElement(0));
            row1 = row1.WithElement(7, rowF.AsInt16().GetElement(0));

            // row2 - E1 D2 C3 B4 A5 A6 B5 C4
            Vector128<short> row2 = Shuffle(rowA, ref masks, 5) | Shuffle(rowB, ref masks, 6) | Shuffle(rowC, ref masks, 7);
            row2 = row2.WithElement(1, rowD.AsInt16().GetElement(2));
            row2 = row2.WithElement(0, rowE.AsInt16().GetElement(1));

            // row3 - D3 E2 F1 G0 H0 G1 F2 E3
            Vector128<short> row3 = Shuffle(rowE, ref masks, 8) | Shuffle(rowF, ref masks, 9) | Shuffle(rowG, ref masks, 10);
            row3 = row3.WithElement(0, rowD.AsInt16().GetElement(3));
            row3 = row3.WithElement(4, rowH.AsInt16().GetElement(0));

            // row4 - D4 C5 B6 A7 B7 C6 D5 E4
            Vector128<short> row4 = Shuffle(rowB, ref masks, 11) | Shuffle(rowC, ref masks, 12) | Shuffle(rowD, ref masks, 13);
            row4 = row4.WithElement(3, rowA.AsInt16().GetElement(7));
            row4 = row4.WithElement(7, rowE.AsInt16().GetElement(4));

            // row5 - F3 G2 H1 H2 G3 F4 E5 D6
            Vector128<short> row5 = Shuffle(rowF, ref masks, 14) | Shuffle(rowG, ref masks, 15) | Shuffle(rowH, ref masks, 16);
            row5 = row5.WithElement(7, rowD.AsInt16().GetElement(6));
            row5 = row5.WithElement(6, rowE.AsInt16().GetElement(5));

            // row6 - C7 D7 E6 F5 G4 H3 H4 G5
            Vector128<short> row6 = Shuffle(rowG, ref masks, 17) | Shuffle(rowH, ref masks, 18);
            row6 = row6.WithElement(0, rowC.AsInt16().GetElement(7));
            row6 = row6.WithElement(1, rowD.AsInt16().GetElement(7));
            row6 = row6.WithElement(2, rowE.AsInt16().GetElement(6));
            row6 = row6.WithElement(3, rowF.AsInt16().GetElement(5));

            // row7 - F6 E7 F7 G6 H5 H6 G7 H7
            Vector128<short> row7 = Shuffle(rowF, ref masks, 19) | Shuffle(rowG, ref masks, 20) | Shuffle(rowH, ref masks, 21);
            row7 = row7.WithElement(1, rowE.AsInt16().GetElement(7));

            v0 = row0;
            v1 = row1;
            v2 = row2;
            v3 = row3;
            v4 = row4;
            v5 = row5;
            v6 = row6;
            v7 = row7;

            // Shuffles the bytes of one row with mask number maskIndex. A mask byte with the high bit set gives zero:
            // pshufb and tbl both do this. Thus, ShuffleNative gives the documented result without a fix-up.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Vector128<short> Shuffle(Vector128<byte> row, ref byte masks, nuint maskIndex)
                => Vector128.ShuffleNative(row, Vector128.LoadUnsafe(ref masks, maskIndex * 16)).AsInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref Vector256<short> v01, ref Vector256<short> v23, ref Vector256<short> v45, ref Vector256<short> v67)
        {
            // Each register holds two rows: AB, CD, EF and GH. Each output register holds two output rows.
            // Step 1: a 32-bit element permute moves each needed value pair into the 128-bit half of its output row.
            // Step 2: a byte shuffle inside each 128-bit half moves each value to its place and sets the other bytes to zero.
            // Step 3: the OR of the shuffled registers gives the output rows.
            // Some permutes serve two output registers. The code keeps those results for the second use.
            ref byte masks = ref MemoryMarshal.GetReference(AvxShuffleMasks);
            Vector256<byte> rowAB = v01.AsByte();
            Vector256<byte> rowCD = v23.AsByte();
            Vector256<byte> rowEF = v45.AsByte();
            Vector256<byte> rowGH = v67.AsByte();

            /* row01 - A0 B0 A1 A2 B1 C0 D0 C1 | B2 A3 A4 B3 C2 D1 E0 F0 */
            Vector256<int> crln_01_AB_CD = Mask(ref masks, 0).AsInt32();
            Vector256<byte> row01_AB = Vector256_.ShufflePerLane(Permute(rowAB, crln_01_AB_CD), Mask(ref masks, 1));
            Vector256<byte> row01_CD = Vector256_.ShufflePerLane(Permute(rowCD, crln_01_AB_CD), Mask(ref masks, 2));
            Vector256<int> crln_01_23_EF_23_CD = Mask(ref masks, 3).AsInt32();
            Vector256<byte> row01_23_EF = Permute(rowEF, crln_01_23_EF_23_CD);
            Vector256<byte> row01_EF = Vector256_.ShufflePerLane(row01_23_EF, Mask(ref masks, 4));

            Vector256<byte> row01 = row01_AB | row01_CD | row01_EF;

            /* row23 - E1 D2 C3 B4 A5 A6 B5 C4 | D3 E2 F1 G0 H0 G1 F2 E3 */
            Vector256<int> crln_23_AB_23_45_GH = Mask(ref masks, 5).AsInt32();
            Vector256<byte> row23_45_AB = Permute(rowAB, crln_23_AB_23_45_GH);
            Vector256<byte> row23_AB = Vector256_.ShufflePerLane(row23_45_AB, Mask(ref masks, 6));
            Vector256<byte> row23_CD = Vector256_.ShufflePerLane(Permute(rowCD, crln_01_23_EF_23_CD), Mask(ref masks, 7));
            Vector256<byte> row23_EF = Vector256_.ShufflePerLane(row01_23_EF, Mask(ref masks, 8));
            Vector256<byte> row23_45_GH = Permute(rowGH, crln_23_AB_23_45_GH);
            Vector256<byte> row23_GH = Vector256_.ShufflePerLane(row23_45_GH, Mask(ref masks, 9));

            Vector256<byte> row23 = (row23_AB | row23_CD) | (row23_EF | row23_GH);

            /* row45 - D4 C5 B6 A7 B7 C6 D5 E4 | F3 G2 H1 H2 G3 F4 E5 D6 */
            Vector256<byte> row45_AB = Vector256_.ShufflePerLane(row23_45_AB, Mask(ref masks, 10));
            Vector256<int> crln_45_67_CD_45_EF = Mask(ref masks, 11).AsInt32();
            Vector256<byte> row45_67_CD = Permute(rowCD, crln_45_67_CD_45_EF);
            Vector256<byte> row45_CD = Vector256_.ShufflePerLane(row45_67_CD, Mask(ref masks, 12));
            Vector256<byte> row45_EF = Vector256_.ShufflePerLane(Permute(rowEF, crln_45_67_CD_45_EF), Mask(ref masks, 13));
            Vector256<byte> row45_GH = Vector256_.ShufflePerLane(row23_45_GH, Mask(ref masks, 14));

            Vector256<byte> row45 = (row45_AB | row45_CD) | (row45_EF | row45_GH);

            /* row67 - C7 D7 E6 F5 G4 H3 H4 G5 | F6 E7 F7 G6 H5 H6 G7 H7 */
            Vector256<byte> row67_CD = Vector256_.ShufflePerLane(row45_67_CD, Mask(ref masks, 15));
            Vector256<int> crln_67_EF_67_GH = Mask(ref masks, 16).AsInt32();
            Vector256<byte> row67_EF = Vector256_.ShufflePerLane(Permute(rowEF, crln_67_EF_67_GH), Mask(ref masks, 17));
            Vector256<byte> row67_GH = Vector256_.ShufflePerLane(Permute(rowGH, crln_67_EF_67_GH), Mask(ref masks, 18));

            Vector256<byte> row67 = row67_CD | (row67_EF | row67_GH);

            v01 = row01.AsInt16();
            v23 = row23.AsInt16();
            v45 = row45.AsInt16();
            v67 = row67.AsInt16();

            // Mask number maskIndex is 32 bytes.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Vector256<byte> Mask(ref byte masks, nuint maskIndex) => Vector256.LoadUnsafe(ref masks, maskIndex * 32);

            // All permute indices are lane numbers 0 to 7. Thus, ShuffleNative is one vpermd instruction.
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static Vector256<byte> Permute(Vector256<byte> rows, Vector256<int> lanes) => Vector256.ShuffleNative(rows.AsInt32(), lanes).AsByte();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(ref Vector512<short> v0123, ref Vector512<short> v4567)
        {
            // The two registers hold all 64 values: v0123 holds values 0 to 31 and v4567 holds values 32 to 63.
            // A two-source 16-bit permute selects any of the 64 values for each output lane.
            // Thus, each output register is one permute, with the transposing order as the index vector:
            // index i of the low output is TransposingOrder[i], and index i of the high output is TransposingOrder[32 + i].
            Vector512<short> lowOrder = Vector512.Create(
                (short)0, 8, 1, 2, 9, 16, 24, 17, 10, 3, 4, 11, 18, 25, 32, 40, 33, 26, 19, 12, 5, 6, 13, 20, 27, 34, 41, 48, 56, 49, 42, 35);
            Vector512<short> highOrder = Vector512.Create(
                (short)28, 21, 14, 7, 15, 22, 29, 36, 43, 50, 57, 58, 51, 44, 37, 30, 23, 31, 38, 45, 52, 59, 60, 53, 46, 39, 47, 54, 61, 62, 55, 63);

            Vector512<short> low = Vector512_.PermuteVar32x16x2(v0123, lowOrder, v4567);
            Vector512<short> high = Vector512_.PermuteVar32x16x2(v0123, highOrder, v4567);

            v0123 = low;
            v4567 = high;
        }
    }
}
