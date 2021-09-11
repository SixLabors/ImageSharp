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
            // A B C
            0, 1, 2, 3, _, _, _, _, _, _, 4, 5, 6, 7, _, _,
            _, _, _, _, 0, 1, _, _, 2, 3, _, _, _, _, 4, 5,
            _, _, _, _, _, _, 0, 1, _, _, _, _, _, _, _, _,

            // row1
            // A B C D E
            _, _, _, _, _, _, _, _, _, _, _, _, 8, 9, 10, 11,
            _, _, _, _, _, _, _, _, _, _, 6, 7, _, _, _, _,
            2, 3, _, _, _, _, _, _, 4, 5, _, _, _, _, _, _,
            _, _, 0, 1, _, _, 2, 3, _, _, _, _, _, _, _, _,
            _, _, _, _, 0, 1, _, _, _, _, _, _, _, _, _, _,

            // row2
            // B C D E F G
            8, 9, _, _, _, _, _, _, _, _, _, _, _, _, _, _,
            _, _, 6, 7, _, _, _, _, _, _, _, _, _, _, _, _,
            _, _, _, _, 4, 5, _, _, _, _, _, _, _, _, _, _,
            _, _, _, _, _, _, 2, 3, _, _, _, _, _, _, 4, 5,
            _, _, _, _, _, _, _, _, 0, 1, _, _, 2, 3, _, _,
            _, _, _, _, _, _, _, _, _, _, 0, 1, _, _, _, _,

            // row3
            // A B C D
            // D shuffle mask is the for row4 E row shuffle mask
            _, _, _, _, _, _, 12, 13, 14, 15, _, _, _, _, _, _,
            _, _, _, _, 10, 11, _, _, _, _, 12, 13, _, _, _, _,
            _, _, 8, 9, _, _, _, _, _, _, _, _, 10, 11, _, _,
            6, 7, _, _, _, _, _, _, _, _, _, _, _, _, 8, 9,

            // row4
            // E F G H
            // 6, 7, _, _, _, _, _, _, _, _, _, _, _, _, 8, 9,
            _, _, 4, 5, _, _, _, _, _, _, _, _, 6, 7, _, _,
            _, _, _, _, 2, 3, _, _, _, _, 4, 5, _, _, _, _,
            _, _, _, _, _, _, 0, 1, 2, 3, _, _, _, _, _, _,

            // row5
            // B C D E F G
            _, _, _, _, 14, 15, _, _, _, _, _, _, _, _, _, _,
            _, _, 12, 13, _, _, 14, 15, _, _, _, _, _, _, _, _,
            10, 11, _, _, _, _, _, _, 12, 13, _, _, _, _, _, _,
            _, _, _, _, _, _, _, _, _, _, 10, 11, _, _, _, _,
            _, _, _, _, _, _, _, _, _, _, _, _, 8, 9, _, _,
            _, _, _, _, _, _, _, _, _, _, _, _, _, _, 6, 7,

            // row6
            // D E F G H
            _, _, _, _, _, _, _, _, _, _, 14, 15, _, _, _, _,
            _, _, _, _, _, _, _, _, 12, 13, _, _, 14, 15, _, _,
            _, _, _, _, _, _, 10, 11, _, _, _, _, _, _, 12, 13,
            _, _, _, _, 8, 9, _, _, _, _, _, _, _, _, _, _,
            4, 5, 6, 7, _, _, _, _, _, _, _, _, _, _, _, _,

            // row7
            // F G H
            _, _, _, _, _, _, _, _, 14, 15, _, _, _, _, _, _,
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
        /// <param name="source">Input matrix.</param>
        /// <param name="dest">Matrix to store the result. Can be a reference to input matrix.</param>
        public static unsafe void ApplyZigZagOrderingSse(ref Block8x8 source, ref Block8x8 dest)
        {
            DebugGuard.IsTrue(Ssse3.IsSupported, "Ssse3 support is required to run this operation!");

            fixed (byte* maskPtr = SseShuffleMasks)
            {
                Vector128<byte> rowA = source.V0.AsByte();
                Vector128<byte> rowB = source.V1.AsByte();
                Vector128<byte> rowC = source.V2.AsByte();
                Vector128<byte> rowD = source.V3.AsByte();
                Vector128<byte> rowE = source.V4.AsByte();
                Vector128<byte> rowF = source.V5.AsByte();
                Vector128<byte> rowG = source.V6.AsByte();
                Vector128<byte> rowH = source.V7.AsByte();

                // row0
                Vector128<short> row0A = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (0 * 16))).AsInt16();
                Vector128<short> row0B = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (1 * 16))).AsInt16();
                Vector128<short> row0 = Sse2.Or(row0A, row0B);
                Vector128<short> row0C = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (2 * 16))).AsInt16();
                row0 = Sse2.Or(row0, row0C);

                // row1
                Vector128<short> row1A = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (3 * 16))).AsInt16();
                Vector128<short> row1B = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (4 * 16))).AsInt16();
                Vector128<short> row1 = Sse2.Or(row1A, row1B);
                Vector128<short> row1C = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (5 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1C);
                Vector128<short> row1D = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (6 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1D);
                Vector128<short> row1E = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (7 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1E);

                // row2
                Vector128<short> row2B = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (8 * 16))).AsInt16();
                Vector128<short> row2C = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (9 * 16))).AsInt16();
                Vector128<short> row2 = Sse2.Or(row2B, row2C);
                Vector128<short> row2D = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (10 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2D);
                Vector128<short> row2E = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (11 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2E);
                Vector128<short> row2F = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (12 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2F);
                Vector128<short> row2G = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (13 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2G);

                // row3
                Vector128<short> row3A = Ssse3.Shuffle(rowA, Sse2.LoadVector128(maskPtr + (14 * 16))).AsInt16().AsInt16();
                Vector128<short> row3B = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (15 * 16))).AsInt16().AsInt16();
                Vector128<short> row3 = Sse2.Or(row3A, row3B);
                Vector128<short> row3C = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (16 * 16))).AsInt16();
                row3 = Sse2.Or(row3, row3C);
                Vector128<byte> row3D_row4E_shuffleMask = Sse2.LoadVector128(maskPtr + (17 * 16));
                Vector128<short> row3D = Ssse3.Shuffle(rowD, row3D_row4E_shuffleMask).AsInt16();
                row3 = Sse2.Or(row3, row3D);

                // row4
                Vector128<short> row4E = Ssse3.Shuffle(rowE, row3D_row4E_shuffleMask).AsInt16();
                Vector128<short> row4F = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (18 * 16))).AsInt16();
                Vector128<short> row4 = Sse2.Or(row4E, row4F);
                Vector128<short> row4G = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (19 * 16))).AsInt16();
                row4 = Sse2.Or(row4, row4G);
                Vector128<short> row4H = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (20 * 16))).AsInt16();
                row4 = Sse2.Or(row4, row4H);

                // row5
                Vector128<short> row5B = Ssse3.Shuffle(rowB, Sse2.LoadVector128(maskPtr + (21 * 16))).AsInt16();
                Vector128<short> row5C = Ssse3.Shuffle(rowC, Sse2.LoadVector128(maskPtr + (22 * 16))).AsInt16();
                Vector128<short> row5 = Sse2.Or(row5B, row5C);
                Vector128<short> row5D = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (23 * 16))).AsInt16();
                row5 = Sse2.Or(row5, row5D);
                Vector128<short> row5E = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (24 * 16))).AsInt16();
                row5 = Sse2.Or(row5, row5E);
                Vector128<short> row5F = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (25 * 16))).AsInt16();
                row5 = Sse2.Or(row5, row5F);
                Vector128<short> row5G = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (26 * 16))).AsInt16();
                row5 = Sse2.Or(row5, row5G);

                // row6
                Vector128<short> row6D = Ssse3.Shuffle(rowD, Sse2.LoadVector128(maskPtr + (27 * 16))).AsInt16();
                Vector128<short> row6E = Ssse3.Shuffle(rowE, Sse2.LoadVector128(maskPtr + (28 * 16))).AsInt16();
                Vector128<short> row6 = Sse2.Or(row6D, row6E);
                Vector128<short> row6F = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (29 * 16))).AsInt16();
                row6 = Sse2.Or(row6, row6F);
                Vector128<short> row6G = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (30 * 16))).AsInt16();
                row6 = Sse2.Or(row6, row6G);
                Vector128<short> row6H = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (31 * 16))).AsInt16();
                row6 = Sse2.Or(row6, row6H);

                // row7
                Vector128<short> row7F = Ssse3.Shuffle(rowF, Sse2.LoadVector128(maskPtr + (32 * 16))).AsInt16();
                Vector128<short> row7G = Ssse3.Shuffle(rowG, Sse2.LoadVector128(maskPtr + (33 * 16))).AsInt16();
                Vector128<short> row7 = Sse2.Or(row7F, row7G);
                Vector128<short> row7H = Ssse3.Shuffle(rowH, Sse2.LoadVector128(maskPtr + (35 * 16))).AsInt16();
                row7 = Sse2.Or(row7, row7H);

                dest.V0 = row0;
                dest.V1 = row1;
                dest.V2 = row2;
                dest.V3 = row3;
                dest.V4 = row4;
                dest.V5 = row5;
                dest.V6 = row6;
                dest.V7 = row7;
            }
        }

        /// <summary>
        /// Applies zig zag ordering for given 8x8 matrix using AVX cpu intrinsics.
        /// </summary>
        /// <remarks>
        /// Requires Avx2 support.
        /// </remarks>
        /// <param name="source">Input matrix.</param>
        /// <param name="dest">Matrix to store the result. Can be a reference to input matrix.</param>
        public static unsafe void ApplyZigZagOrderingAvx(ref Block8x8 source, ref Block8x8 dest)
        {
            DebugGuard.IsTrue(Avx2.IsSupported, "Avx2 support is required to run this operation!");

            fixed (byte* shuffleVectorsPtr = AvxShuffleMasks)
            {
                Vector256<byte> rowsAB = source.V01.AsByte();
                Vector256<byte> rowsCD = source.V23.AsByte();
                Vector256<byte> rowsEF = source.V45.AsByte();
                Vector256<byte> rowsGH = source.V67.AsByte();

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

                dest.V01 = row01.AsInt16();
                dest.V23 = row23.AsInt16();
                dest.V45 = row45.AsInt16();
                dest.V67 = row67.AsInt16();
            }
        }
    }
}
#endif