// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal static partial class ZigZag
    {
#pragma warning disable SA1309 // naming rules violation warnings
        /// <summary>
        /// Special byte value to zero out elements during Sse/Avx shuffle intrinsics.
        /// </summary>
        private const byte _ = 0xff;
#pragma warning restore SA1309

        /// <summary>
        /// Gets shuffle vectors for <see cref="ApplyZigZagOrderingSse"/>
        /// zig zag implementation.
        /// </summary>
        private static ReadOnlySpan<byte> SseShuffleMasks => new byte[]
        {
            // row0
            0, 1, 2, 3, _, _, _, _, _, _, 4, 5, 6, 7, _, _,
            _, _, _, _, 0, 1, _, _, 2, 3, _, _, _, _, 4, 5,
            _, _, _, _, _, _, 0, 1, _, _, _, _, _, _, _, _,

            // row1
            _, _, _, _, _, _, _, _, _, _, _, _, 8, 9, 10, 11,
            2, 3, _, _, _, _, _, _, 4, 5, _, _, _, _, _, _,
            _, _, 0, 1, _, _, 2, 3, _, _, _, _, _, _, _, _,

            // row2
            _, _, _, _, _, _, 2, 3, _, _, _, _, _, _, 4, 5,
            _, _, _, _, _, _, _, _, 0, 1, _, _, 2, 3, _, _,

            // row3
            _, _, _, _, _, _, 12, 13, 14, 15, _, _, _, _, _, _,
            _, _, _, _, 10, 11, _, _, _, _, 12, 13, _, _, _, _,
            _, _, 8, 9, _, _, _, _, _, _, _, _, 10, 11, _, _,
            6, 7, _, _, _, _, _, _, _, _, _, _, _, _, 8, 9,

            // row4
            _, _, 4, 5, _, _, _, _, _, _, _, _, 6, 7, _, _,
            _, _, _, _, 2, 3, _, _, _, _, 4, 5, _, _, _, _,
            _, _, _, _, _, _, 0, 1, 2, 3, _, _, _, _, _, _,

            // row5
            _, _, 12, 13, _, _, 14, 15, _, _, _, _, _, _, _, _,
            10, 11, _, _, _, _, _, _, 12, 13, _, _, _, _, _, _,

            // row6
            _, _, _, _, _, _, _, _, 12, 13, _, _, 14, 15, _, _,
            _, _, _, _, _, _, 10, 11, _, _, _, _, _, _, 12, 13,
            4, 5, 6, 7, _, _, _, _, _, _, _, _, _, _, _, _,

            // row7
            10, 11, _, _, _, _, 12, 13, _, _, 14, 15, _, _, _, _,
            _, _, 8, 9, 10, 11, _, _, _, _, _, _, 12, 13, 14, 15
        };

        /// <summary>
        /// Gets shuffle vectors for <see cref="ApplyZigZagOrderingAvx"/>
        /// zig zag implementation.
        /// </summary>
        private static ReadOnlySpan<byte> AvxShuffleMasks => new byte[]
        {
                // 01_AB/01_EF/23_CD - cross-lane
                0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   5, 0, 0, 0,   0, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   6, 0, 0, 0,

                // 01_AB - inner-lane
                0, 1, 2, 3,   8, 9, _, _,   10, 11, 4, 5,   6, 7, 12, 13,  _, _, _, _,   _, _, _, _,   _, _, 10, 11,   4, 5, 6, 7,

                // 01_CD/23_GH - cross-lane
                0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   _, _, _, _,   0, 0, 0, 0,   1, 0, 0, 0,   4, 0, 0, 0,   _, _, _, _,

                // 01_CD - inner-lane
                _, _, _, _,   _, _, 0, 1,   _, _, _, _,   _, _, _, _,   2, 3, 8, 9,   _, _, 10, 11,   4, 5, _, _,   _, _, _, _,

                // 01_EF - inner-lane
                _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   0, 1, _, _,   _, _, _, _,   _, _, _, _,

                // 23_AB/45_CD/67_EF - cross-lane
                3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   _, _, _, _,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,   _, _, _, _,

                // 23_AB - inner-lane
                4, 5, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   6, 7, 0, 1,   2, 3, 8, 9,   _, _, _, _,

                // 23_CD - inner-lane
                _, _, 6, 7,   12, 13, _, _,   _, _, _, _,   _, _, _, _,   10, 11, 4, 5,   _, _, _, _,   _, _, _, _,   6, 7, 12, 13,

                // 23_EF - inner-lane
                _, _, _, _,   _, _, 2, 3,   8, 9, _, _,   10, 11, 4, 5,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,

                // 23_GH - inner-lane
                _, _, _, _,   _, _, _, _,   _, _, 0, 1,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,

                // 45_AB - inner-lane
                _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   10, 11, _, _,   _, _, _, _,   _, _, _, _,

                // 45_CD - inner-lane
                _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   6, 7, 0, 1,   _, _, 2, 3,   8, 9, _, _,   _, _, _, _,

                // 45_EF - cross-lane
                1, 0, 0, 0,   2, 0, 0, 0,   5, 0, 0, 0,   _, _, _, _,   2, 0, 0, 0,   3, 0, 0, 0,   6, 0, 0, 0,   7, 0, 0, 0,

                // 45_EF - inner-lane
                2, 3, 8, 9,   _, _, _, _,   _, _, _, _,   10, 11, 4, 5,  _, _, _, _,   _, _, _, _,   _, _, 2, 3,   8, 9, _, _,

                // 45_GH - inner-lane
                _, _, _, _,   2, 3, 8, 9,   10, 11, 4, 5,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, 6, 7,

                // 67_CD - inner-lane
                _, _, _, _,   _, _, _, _,   _, _, 10, 11,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,   _, _, _, _,

                // 67_EF - inner-lane
                _, _, _, _,   _, _, 6, 7,   0, 1, _, _,   2, 3, 8, 9,   _, _, _, _,   _, _, _, _,   10, 11, _, _,   _, _, _, _,

                // 67_GH - inner-lane
                8, 9, 10, 11,   4, 5, _, _,   _, _, _, _,   _, _, _, _,   2, 3, 8, 9,   10, 11, 4, 5,   _, _, 6, 7,   12, 13, 14, 15
        };

        /// <summary>
        /// Applies zig zag ordering for given 8x8 matrix using SSE cpu intrinsics.
        /// </summary>
        /// <remarks>
        /// Requires Ssse3 support.
        /// </remarks>
        /// <param name="block">Input matrix.</param>
        public static unsafe void ApplyZigZagOrderingSse(ref Block8x8 block)
        {
            DebugGuard.IsTrue(Ssse3.IsSupported, "Ssse3 support is required to run this operation!");

            fixed (byte* maskPtr = SseShuffleMasks)
            {
                Vector128<byte> rowA = block.V0.AsByte();
                Vector128<byte> rowB = block.V1.AsByte();
                Vector128<byte> rowC = block.V2.AsByte();
                Vector128<byte> rowD = block.V3.AsByte();
                Vector128<byte> rowE = block.V4.AsByte();
                Vector128<byte> rowF = block.V5.AsByte();
                Vector128<byte> rowG = block.V6.AsByte();
                Vector128<byte> rowH = block.V7.AsByte();

                // row0 - A0  A1  B0  C0  B1  A2  A3  B2
                Vector128<short> rowA0 = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (16 * 0))).AsInt16();
                Vector128<short> rowB0 = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (16 * 1))).AsInt16();
                Vector128<short> row0 = Sse2.Or(rowA0, rowB0);
                Vector128<short> rowC0 = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (16 * 2))).AsInt16();
                row0 = Sse2.Or(row0, rowC0);

                // row1 - C1  D0  E0  D1  C2  B3  A4  A5
                Vector128<short> rowA1 = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (16 * 3))).AsInt16();
                Vector128<short> rowC1 = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (16 * 4))).AsInt16();
                Vector128<short> row1 = Sse2.Or(rowA1, rowC1);
                Vector128<short> rowD1 = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (16 * 5))).AsInt16();
                row1 = Sse2.Or(row1, rowD1);
                row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowB.AsUInt16(), 3), 5).AsInt16();
                row1 = Sse2.Insert(row1.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 0), 2).AsInt16();

                // row2
                Vector128<short> rowE2 = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (16 * 6))).AsInt16();
                Vector128<short> rowF2 = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (16 * 7))).AsInt16();
                Vector128<short> row2 = Sse2.Or(rowE2, rowF2);
                row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowB.AsUInt16(), 4), 0).AsInt16();
                row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowC.AsUInt16(), 3), 1).AsInt16();
                row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 2), 2).AsInt16();
                row2 = Sse2.Insert(row2.AsUInt16(), Sse2.Extract(rowG.AsUInt16(), 0), 5).AsInt16();

                // row3
                Vector128<short> rowA3 = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (16 * 8))).AsInt16().AsInt16();
                Vector128<short> rowB3 = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (16 * 9))).AsInt16().AsInt16();
                Vector128<short> row3 = Sse2.Or(rowA3, rowB3);
                Vector128<short> rowC3 = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (16 * 10))).AsInt16();
                row3 = Sse2.Or(row3, rowC3);
                Vector128<byte> shuffleRowD3EF = Sse2.LoadVector128(maskPtr + (16 * 11));
                Vector128<short> rowD3 = Ssse3.Shuffle(rowD, shuffleRowD3EF).AsInt16();
                row3 = Sse2.Or(row3, rowD3);

                // row4
                Vector128<short> rowE4 = Ssse3.Shuffle(rowE, shuffleRowD3EF).AsInt16();
                Vector128<short> rowF4 = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (16 * 12))).AsInt16();
                Vector128<short> row4 = Sse2.Or(rowE4, rowF4);
                Vector128<short> rowG4 = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (16 * 13))).AsInt16();
                row4 = Sse2.Or(row4, rowG4);
                Vector128<short> rowH4 = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (16 * 14))).AsInt16();
                row4 = Sse2.Or(row4, rowH4);

                // row5
                Vector128<short> rowC5 = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (16 * 15))).AsInt16();
                Vector128<short> rowD5 = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (16 * 16))).AsInt16();
                Vector128<short> row5 = Sse2.Or(rowC5, rowD5);
                row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowB.AsUInt16(), 7), 2).AsInt16();
                row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowE.AsUInt16(), 5), 5).AsInt16();
                row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowF.AsUInt16(), 4), 6).AsInt16();
                row5 = Sse2.Insert(row5.AsUInt16(), Sse2.Extract(rowG.AsUInt16(), 3), 7).AsInt16();

                // row6
                Vector128<short> rowE6 = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (16 * 17))).AsInt16();
                Vector128<short> rowF6 = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (16 * 18))).AsInt16();
                Vector128<short> row6 = Sse2.Or(rowE6, rowF6);
                Vector128<short> rowH6 = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (16 * 19))).AsInt16();
                row6 = Sse2.Or(row6, rowH6);
                row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowD.AsUInt16(), 7), 5).AsInt16();
                row6 = Sse2.Insert(row6.AsUInt16(), Sse2.Extract(rowG.AsUInt16(), 4), 2).AsInt16();

                // row7
                Vector128<short> rowG7 = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (16 * 20))).AsInt16();
                Vector128<short> rowH7 = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (16 * 21))).AsInt16();
                Vector128<short> row7 = Sse2.Or(rowG7, rowH7);
                row7 = Sse2.Insert(row7.AsUInt16(), Sse2.Extract(rowF.AsUInt16(), 7), 4).AsInt16();

                block.V0 = row0;
                block.V1 = row1;
                block.V2 = row2;
                block.V3 = row3;
                block.V4 = row4;
                block.V5 = row5;
                block.V6 = row6;
                block.V7 = row7;
            }
        }

        /// <summary>
        /// Applies zig zag ordering for given 8x8 matrix using AVX cpu intrinsics.
        /// </summary>
        /// <remarks>
        /// Requires Avx2 support.
        /// </remarks>
        /// <param name="block">Input matrix.</param>
        public static unsafe void ApplyZigZagOrderingAvx(ref Block8x8 block)
        {
            DebugGuard.IsTrue(Avx2.IsSupported, "Avx2 support is required to run this operation!");

            fixed (byte* shuffleVectorsPtr = AvxShuffleMasks)
            {
                Vector256<byte> rowsAB = block.V01.AsByte();
                Vector256<byte> rowsCD = block.V23.AsByte();
                Vector256<byte> rowsEF = block.V45.AsByte();
                Vector256<byte> rowsGH = block.V67.AsByte();

                // rows 0 1
                Vector256<int> rows_AB01_EF01_CD23_shuffleMask = Avx.LoadVector256(shuffleVectorsPtr + (0 * 32)).AsInt32();
                Vector256<byte> row01_AB = Avx2.PermuteVar8x32(rowsAB.AsInt32(), rows_AB01_EF01_CD23_shuffleMask).AsByte();
                row01_AB = Avx2.Shuffle(row01_AB, Avx.LoadVector256(shuffleVectorsPtr + (1 * 32))).AsByte();

                Vector256<int> rows_CD01_GH23_shuffleMask = Avx.LoadVector256(shuffleVectorsPtr + (2 * 32)).AsInt32();
                Vector256<byte> row01_CD = Avx2.PermuteVar8x32(rowsCD.AsInt32(), rows_CD01_GH23_shuffleMask).AsByte();
                row01_CD = Avx2.Shuffle(row01_CD, Avx.LoadVector256(shuffleVectorsPtr + (3 * 32))).AsByte();

                Vector256<byte> row0123_EF = Avx2.PermuteVar8x32(rowsEF.AsInt32(), rows_AB01_EF01_CD23_shuffleMask).AsByte();
                Vector256<byte> row01_EF = Avx2.Shuffle(row0123_EF, Avx.LoadVector256(shuffleVectorsPtr + (4 * 32))).AsByte();

                Vector256<byte> row01 = Avx2.Or(Avx2.Or(row01_AB, row01_CD), row01_EF);

                // rows 2 3
                Vector256<int> rows_AB23_CD45_EF67_shuffleMask = Avx.LoadVector256(shuffleVectorsPtr + (5 * 32)).AsInt32();
                Vector256<byte> row2345_AB = Avx2.PermuteVar8x32(rowsAB.AsInt32(), rows_AB23_CD45_EF67_shuffleMask).AsByte();
                Vector256<byte> row23_AB = Avx2.Shuffle(row2345_AB, Avx.LoadVector256(shuffleVectorsPtr + (6 * 32))).AsByte();

                Vector256<byte> row23_CD = Avx2.PermuteVar8x32(rowsCD.AsInt32(), rows_AB01_EF01_CD23_shuffleMask).AsByte();
                row23_CD = Avx2.Shuffle(row23_CD, Avx.LoadVector256(shuffleVectorsPtr + (7 * 32))).AsByte();

                Vector256<byte> row23_EF = Avx2.Shuffle(row0123_EF, Avx.LoadVector256(shuffleVectorsPtr + (8 * 32))).AsByte();

                Vector256<byte> row2345_GH = Avx2.PermuteVar8x32(rowsGH.AsInt32(), rows_CD01_GH23_shuffleMask).AsByte();
                Vector256<byte> row23_GH = Avx2.Shuffle(row2345_GH, Avx.LoadVector256(shuffleVectorsPtr + (9 * 32)).AsByte());

                Vector256<byte> row23 = Avx2.Or(Avx2.Or(row23_AB, row23_CD), Avx2.Or(row23_EF, row23_GH));

                // rows 4 5
                Vector256<byte> row45_AB = Avx2.Shuffle(row2345_AB, Avx.LoadVector256(shuffleVectorsPtr + (10 * 32)).AsByte());
                Vector256<byte> row4567_CD = Avx2.PermuteVar8x32(rowsCD.AsInt32(), rows_AB23_CD45_EF67_shuffleMask).AsByte();
                Vector256<byte> row45_CD = Avx2.Shuffle(row4567_CD, Avx.LoadVector256(shuffleVectorsPtr + (11 * 32)).AsByte());

                Vector256<int> rows_EF45_GH67_shuffleMask = Avx.LoadVector256(shuffleVectorsPtr + (12 * 32)).AsInt32();
                Vector256<byte> row45_EF = Avx2.PermuteVar8x32(rowsEF.AsInt32(), rows_EF45_GH67_shuffleMask).AsByte();
                row45_EF = Avx2.Shuffle(row45_EF, Avx.LoadVector256(shuffleVectorsPtr + (13 * 32)).AsByte());

                Vector256<byte> row45_GH = Avx2.Shuffle(row2345_GH, Avx.LoadVector256(shuffleVectorsPtr + (14 * 32)).AsByte());

                Vector256<byte> row45 = Avx2.Or(Avx2.Or(row45_AB, row45_CD), Avx2.Or(row45_EF, row45_GH));

                // rows 6 7
                Vector256<byte> row67_CD = Avx2.Shuffle(row4567_CD, Avx.LoadVector256(shuffleVectorsPtr + (15 * 32)).AsByte());

                Vector256<byte> row67_EF = Avx2.PermuteVar8x32(rowsEF.AsInt32(), rows_AB23_CD45_EF67_shuffleMask).AsByte();
                row67_EF = Avx2.Shuffle(row67_EF, Avx.LoadVector256(shuffleVectorsPtr + (16 * 32)).AsByte());

                Vector256<byte> row67_GH = Avx2.PermuteVar8x32(rowsGH.AsInt32(), rows_EF45_GH67_shuffleMask).AsByte();
                row67_GH = Avx2.Shuffle(row67_GH, Avx.LoadVector256(shuffleVectorsPtr + (17 * 32)).AsByte());

                Vector256<byte> row67 = Avx2.Or(Avx2.Or(row67_CD, row67_EF), row67_GH);

                block.V01 = row01.AsInt16();
                block.V23 = row23.AsInt16();
                block.V45 = row45.AsInt16();
                block.V67 = row67.AsInt16();
            }
        }
    }
}
#endif
