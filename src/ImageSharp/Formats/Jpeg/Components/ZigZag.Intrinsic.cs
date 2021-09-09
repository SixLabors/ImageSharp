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
            // 0_A
            0, 1, 2, 3, _, _, _, _, _, _, 4, 5, 6, 7, _, _,
            // 0_B
            _, _, _, _, 0, 1, _, _, 2, 3, _, _, _, _, 4, 5,
            // 0_C
            _, _, _, _, _, _, 0, 1, _, _, _, _, _, _, _, _,

            // 1_A
            _, _, _, _, _, _, _, _, _, _, _, _, 8, 9, 10, 11,
            // 1_B
            _, _, _, _, _, _, _, _, _, _, 6, 7, _, _, _, _,
            // 1_C
            2, 3, _, _, _, _, _, _, 4, 5, _, _, _, _, _, _,
            // 1_D
            _, _, 0, 1, _, _, 2, 3, _, _, _, _, _, _, _, _,
            // 1_E
            _, _, _, _, 0, 1, _, _, _, _, _, _, _, _, _, _,

            // 2_B
            8, 9, _, _, _, _, _, _, _, _, _, _, _, _, _, _,
            // 2_C
            _, _, 6, 7, _, _, _, _, _, _, _, _, _, _, _, _,
            // 2_D
            _, _, _, _, 4, 5, _, _, _, _, _, _, _, _, _, _,
            // 2_E
            _, _, _, _, _, _, 2, 3, _, _, _, _, _, _, 4, 5,
            // 2_F
            _, _, _, _, _, _, _, _, 0, 1, _, _, 2, 3, _, _,
            // 2_G
            _, _, _, _, _, _, _, _, _, _, 0, 1, _, _, _, _,

            // 3_A
            _, _, _, _, _, _, 12, 13, 14, 15, _, _, _, _, _, _,
            // 3_B
            _, _, _, _, 10, 11, _, _, _, _, 12, 13, _, _, _, _,
            // 3_C
            _, _, 8, 9, _, _, _, _, _, _, _, _, 10, 11, _, _,
            // 3_D/4_E
            6, 7, _, _, _, _, _, _, _, _, _, _, _, _, 8, 9,

            // 4_F
            _, _, 4, 5, _, _, _, _, _, _, _, _, 6, 7, _, _,
            // 4_G
            _, _, _, _, 2, 3, _, _, _, _, 4, 5, _, _, _, _,
            // 4_H
            _, _, _, _, _, _, 0, 1, 2, 3, _, _, _, _, _, _,

            // 5_B
            _, _, _, _, 14, 15, _, _, _, _, _, _, _, _, _, _,
            // 5_C
            _, _, 12, 13, _, _, 14, 15, _, _, _, _, _, _, _, _,
            // 5_D
            10, 11, _, _, _, _, _, _, 12, 13, _, _, _, _, _, _,
            // 5_E
            _, _, _, _, _, _, _, _, _, _, 10, 11, _, _, _, _,
            // 5_F
            _, _, _, _, _, _, _, _, _, _, _, _, 8, 9, _, _,
            // 5_G
            _, _, _, _, _, _, _, _, _, _, _, _, _, _, 6, 7,

            // 6_D
            _, _, _, _, _, _, _, _, _, _, 14, 15, _, _, _, _,
            // 6_E
            _, _, _, _, _, _, _, _, 12, 13, _, _, 14, 15, _, _,
            // 6_F
            _, _, _, _, _, _, 10, 11, _, _, _, _, _, _, 12, 13,
            // 6_G
            _, _, _, _, 8, 9, _, _, _, _, _, _, _, _, _, _,
            // 6_H
            4, 5, 6, 7, _, _, _, _, _, _, _, _, _, _, _, _,

            // 7_F
            _, _, _, _, _, _, _, _, 14, 15, _, _, _, _, _, _,
            // 7_G
            10, 11, _, _, _, _, 12, 13, _, _, 14, 15, _, _, _, _,
            // 7_H
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
                Vector128<byte> A = source.V0.AsByte();
                Vector128<byte> B = source.V1.AsByte();
                Vector128<byte> C = source.V2.AsByte();
                Vector128<byte> D = source.V3.AsByte();
                Vector128<byte> E = source.V4.AsByte();
                Vector128<byte> F = source.V5.AsByte();
                Vector128<byte> G = source.V6.AsByte();
                Vector128<byte> H = source.V7.AsByte();

                // row0
                Vector128<short> row0_A = Ssse3.Shuffle(A, Sse2.LoadVector128(maskPtr + (0 * 16))).AsInt16();
                Vector128<short> row0_B = Ssse3.Shuffle(B, Sse2.LoadVector128(maskPtr + (1 * 16))).AsInt16();
                Vector128<short> row0 = Sse2.Or(row0_A, row0_B);
                Vector128<short> row0_C = Ssse3.Shuffle(C, Sse2.LoadVector128(maskPtr + (2 * 16))).AsInt16();
                row0 = Sse2.Or(row0, row0_C);

                // row1
                Vector128<short> row1_A = Ssse3.Shuffle(A, Sse2.LoadVector128(maskPtr + (3 * 16))).AsInt16();
                Vector128<short> row1_B = Ssse3.Shuffle(B, Sse2.LoadVector128(maskPtr + (4 * 16))).AsInt16();
                Vector128<short> row1 = Sse2.Or(row1_A, row1_B);
                Vector128<short> row1_C = Ssse3.Shuffle(C, Sse2.LoadVector128(maskPtr + (5 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1_C);
                Vector128<short> row1_D = Ssse3.Shuffle(D, Sse2.LoadVector128(maskPtr + (6 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1_D);
                Vector128<short> row1_E = Ssse3.Shuffle(E, Sse2.LoadVector128(maskPtr + (7 * 16))).AsInt16();
                row1 = Sse2.Or(row1, row1_E);

                // row2
                Vector128<short> row2_B = Ssse3.Shuffle(B, Sse2.LoadVector128(maskPtr + (8 * 16))).AsInt16();
                Vector128<short> row2_C = Ssse3.Shuffle(C, Sse2.LoadVector128(maskPtr + (9 * 16))).AsInt16();
                Vector128<short> row2 = Sse2.Or(row2_B, row2_C);
                Vector128<short> row2_D = Ssse3.Shuffle(D, Sse2.LoadVector128(maskPtr + (10 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2_D);
                Vector128<short> row2_E = Ssse3.Shuffle(E, Sse2.LoadVector128(maskPtr + (11 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2_E);
                Vector128<short> row2_F = Ssse3.Shuffle(F, Sse2.LoadVector128(maskPtr + (12 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2_F);
                Vector128<short> row2_G = Ssse3.Shuffle(G, Sse2.LoadVector128(maskPtr + (13 * 16))).AsInt16();
                row2 = Sse2.Or(row2, row2_G);

                // row3
                Vector128<short> A_3 = Ssse3.Shuffle(A, Sse2.LoadVector128(maskPtr + (14 * 16))).AsInt16().AsInt16();
                Vector128<short> B_3 = Ssse3.Shuffle(B, Sse2.LoadVector128(maskPtr + (15 * 16))).AsInt16().AsInt16();
                Vector128<short> row3 = Sse2.Or(A_3, B_3);
                Vector128<short> C_3 = Ssse3.Shuffle(C, Sse2.LoadVector128(maskPtr + (16 * 16))).AsInt16();
                row3 = Sse2.Or(row3, C_3);
                Vector128<byte> D3_E4_shuffleMask = Sse2.LoadVector128(maskPtr + (17 * 16));
                Vector128<short> D_3 = Ssse3.Shuffle(D, D3_E4_shuffleMask).AsInt16();
                row3 = Sse2.Or(row3, D_3);

                // row4
                Vector128<short> E_4 = Ssse3.Shuffle(E, D3_E4_shuffleMask).AsInt16();
                Vector128<short> F_4 = Ssse3.Shuffle(F, Sse2.LoadVector128(maskPtr + (18 * 16))).AsInt16();
                Vector128<short> row4 = Sse2.Or(E_4, F_4);
                Vector128<short> G_4 = Ssse3.Shuffle(G, Sse2.LoadVector128(maskPtr + (19 * 16))).AsInt16();
                row4 = Sse2.Or(row4, G_4);
                Vector128<short> H_4 = Ssse3.Shuffle(H, Sse2.LoadVector128(maskPtr + (20 * 16))).AsInt16();
                row4 = Sse2.Or(row4, H_4);

                // row5
                Vector128<short> B_5 = Ssse3.Shuffle(B, Sse2.LoadVector128(maskPtr + (21 * 16))).AsInt16();
                Vector128<short> C_5 = Ssse3.Shuffle(C, Sse2.LoadVector128(maskPtr + (22 * 16))).AsInt16();
                Vector128<short> row5 = Sse2.Or(B_5, C_5);
                Vector128<short> D_5 = Ssse3.Shuffle(D, Sse2.LoadVector128(maskPtr + (23 * 16))).AsInt16();
                row5 = Sse2.Or(row5, D_5);
                Vector128<short> E_5 = Ssse3.Shuffle(E, Sse2.LoadVector128(maskPtr + (24 * 16))).AsInt16();
                row5 = Sse2.Or(row5, E_5);
                Vector128<short> F_5 = Ssse3.Shuffle(F, Sse2.LoadVector128(maskPtr + (25 * 16))).AsInt16();
                row5 = Sse2.Or(row5, F_5);
                Vector128<short> G_5 = Ssse3.Shuffle(G, Sse2.LoadVector128(maskPtr + (26 * 16))).AsInt16();
                row5 = Sse2.Or(row5, G_5);

                // row6
                Vector128<short> D_6 = Ssse3.Shuffle(D, Sse2.LoadVector128(maskPtr + (27 * 16))).AsInt16();
                Vector128<short> E_6 = Ssse3.Shuffle(E, Sse2.LoadVector128(maskPtr + (28 * 16))).AsInt16();
                Vector128<short> row6 = Sse2.Or(D_6, E_6);
                Vector128<short> F_6 = Ssse3.Shuffle(F, Sse2.LoadVector128(maskPtr + (29 * 16))).AsInt16();
                row6 = Sse2.Or(row6, F_6);
                Vector128<short> G_6 = Ssse3.Shuffle(G, Sse2.LoadVector128(maskPtr + (30 * 16))).AsInt16();
                row6 = Sse2.Or(row6, G_6);
                Vector128<short> H_6 = Ssse3.Shuffle(H, Sse2.LoadVector128(maskPtr + (31 * 16))).AsInt16();
                row6 = Sse2.Or(row6, H_6);

                // row7
                Vector128<short> F_7 = Ssse3.Shuffle(F, Sse2.LoadVector128(maskPtr + (32 * 16))).AsInt16();
                Vector128<short> G_7 = Ssse3.Shuffle(G, Sse2.LoadVector128(maskPtr + (33 * 16))).AsInt16();
                Vector128<short> row7 = Sse2.Or(F_7, G_7);
                Vector128<short> H_7 = Ssse3.Shuffle(H, Sse2.LoadVector128(maskPtr + (35 * 16))).AsInt16();
                row7 = Sse2.Or(row7, H_7);

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
                // 18 loads
                // 10 cross-lane shuffles (permutations)
                // 14 shuffles
                // 10 bitwise or's
                // 4 stores

                // A0 A1 A2 A3 A4 A5 A6 A7 | B0 B1 B2 B3 B4 B5 B6 B7
                // C0 C1 C2 C3 C4 C5 C6 C7 | D0 D1 D2 D3 D4 D5 D6 D7
                // E0 E1 E2 E3 E4 E5 E6 E7 | F0 F1 F2 F3 F4 F5 F6 F7
                // G0 G1 G2 G3 G4 G5 G6 G7 | H0 H1 H2 H3 H4 H5 H6 H7
                Vector256<byte> AB = source.V01.AsByte();
                Vector256<byte> CD = source.V23.AsByte();
                Vector256<byte> EF = source.V45.AsByte();
                Vector256<byte> GH = source.V67.AsByte();

                // row01 - A0  A1  B0  C0  B1  A2  A3  B2 | C1  D0  E0  D1  C2  B3  A4  A5
                Vector256<int> AB01_EF01_CD23_cr_ln_shfmask = Avx.LoadVector256(shuffleVectorsPtr + (0 * 32)).AsInt32();

                // row01_AB - (A0 A1) (B0 B1) (A2 A3) (B2 B3) | (B2 B3) (A4 A5) (X  X)  (X  X)
                Vector256<byte> row01_AB = Avx2.PermuteVar8x32(AB.AsInt32(), AB01_EF01_CD23_cr_ln_shfmask).AsByte();
                // row01_AB - (A0 A1) (B0  X) (B1 A2) (A3 B2) | (X  X)  (X  X)  (X  B3) (A4 A5)
                row01_AB = Avx2.Shuffle(row01_AB, Avx.LoadVector256(shuffleVectorsPtr + (1 * 32))).AsByte();

                Vector256<int> CD01_GH23_cr_ln_shfmask = Avx.LoadVector256(shuffleVectorsPtr + (2 * 32)).AsInt32();

                // row01_CD - (C0 C1) (X X)  (X X) (X X) | (C0 C1) (D0 D1) (C2 C3) (X X)
                Vector256<byte> row01_CD = Avx2.PermuteVar8x32(CD.AsInt32(), CD01_GH23_cr_ln_shfmask).AsByte();
                // row01_CD - (X  X)  (X C0) (X X) (X X) | (C1 D0) (X  D1)  (C2 X)  (X X)
                row01_CD = Avx2.Shuffle(row01_CD, Avx.LoadVector256(shuffleVectorsPtr + (3 * 32))).AsByte();

                // row01_EF - (E0 E1) (E2 E3) (F0 F1) (X X) | (E0 E1) (X X)  (X X) (X X)
                Vector256<byte> row0123_EF = Avx2.PermuteVar8x32(EF.AsInt32(), AB01_EF01_CD23_cr_ln_shfmask).AsByte();
                // row01_EF - (X X) (X X) (X X) (X X) | (X  X)  (E0 X) (X X) (X X)
                Vector256<byte> row01_EF = Avx2.Shuffle(row0123_EF, Avx.LoadVector256(shuffleVectorsPtr + (4 * 32))).AsByte();

                Vector256<byte> row01 = Avx2.Or(Avx2.Or(row01_AB, row01_CD), row01_EF);


                // row23 - B4  C3  D2  E1  F0  G0  F1  E2 | D3  C4  B5  A6  A7  B6  C5  D4

                Vector256<int> AB23_CD45_EF67_cr_ln_shfmask = Avx.LoadVector256(shuffleVectorsPtr + (5 * 32)).AsInt32();

                // row23_AB - (B4 B5) (X X) (X X) (X X) | (B4 B5) (B6 B7) (A6 A7) (X X)
                Vector256<byte> row2345_AB = Avx2.PermuteVar8x32(AB.AsInt32(), AB23_CD45_EF67_cr_ln_shfmask).AsByte();
                // row23_AB - (B4 X) (X X) (X X) (X X) | (X X) (B5 A6) (A7 B6) (X X)
                Vector256<byte> row23_AB = Avx2.Shuffle(row2345_AB, Avx.LoadVector256(shuffleVectorsPtr + (6 * 32))).AsByte();

                // row23_CD - (C2 C3) (D2 D3) (X X) (X X) | (D2 D3) (C4 C5) (D4 D5) (X X)
                Vector256<byte> row23_CD = Avx2.PermuteVar8x32(CD.AsInt32(), AB01_EF01_CD23_cr_ln_shfmask).AsByte();
                // row23_CD - (X C3) (D2 X) (X X) (X X) | (D3 C4) (X X) (X X) (C5 D4)
                row23_CD = Avx2.Shuffle(row23_CD, Avx.LoadVector256(shuffleVectorsPtr + (7 * 32))).AsByte();

                // row23_EF - (X X) (X E1) (F0 X) (F1 E2) | (X X) (X X) (X X) (X X)
                Vector256<byte> row23_EF = Avx2.Shuffle(row0123_EF, Avx.LoadVector256(shuffleVectorsPtr + (8 * 32))).AsByte();

                // row23_GH - (G0 G1) (G2 G3) (H0 H1) (X X) | (G2 G3) (X X) (X X) (X X)
                Vector256<byte> row2345_GH = Avx2.PermuteVar8x32(GH.AsInt32(), CD01_GH23_cr_ln_shfmask).AsByte();
                // row23_GH - (X X) (X X) (X G0) (X X) | (X X) (X X) (X X) (X X)
                Vector256<byte> row23_GH = Avx2.Shuffle(row2345_GH, Avx.LoadVector256(shuffleVectorsPtr + (9 * 32)).AsByte());

                Vector256<byte> row23 = Avx2.Or(Avx2.Or(row23_AB, row23_CD), Avx2.Or(row23_EF, row23_GH));


                // row45 - E3  F2  G1  H0  H1  G2  F3  E4 | D5  C6  B7  C7  D6  E5  F4  G3

                // row45_AB - (X X) (X X) (X X) (X X) | (X X) (B7 X) (X X) (X X)
                Vector256<byte> row45_AB = Avx2.Shuffle(row2345_AB, Avx.LoadVector256(shuffleVectorsPtr + (10 * 32)).AsByte());

                // row45_CD - (D6 D7) (X X) (X X) (X X) | (C6 C7) (D4 D5) (D6 D7) (X X)
                Vector256<byte> row4567_CD = Avx2.PermuteVar8x32(CD.AsInt32(), AB23_CD45_EF67_cr_ln_shfmask).AsByte();
                // row45_CD - (X X) (X X) (X X) (X X) | (D5 C6) (X C7) (D6 X) (X X)
                Vector256<byte> row45_CD = Avx2.Shuffle(row4567_CD, Avx.LoadVector256(shuffleVectorsPtr + (11 * 32)).AsByte());

                Vector256<int> EF45_GH67_cr_ln_shfmask = Avx.LoadVector256(shuffleVectorsPtr + (12 * 32)).AsInt32();

                // row45_EF - (E2 E3) (E4 E5) (F2 F3) (X X) | (E4 E5) (F4 F5) (X X) (X X)
                Vector256<byte> row45_EF = Avx2.PermuteVar8x32(EF.AsInt32(), EF45_GH67_cr_ln_shfmask).AsByte();
                // row45_EF - (E3 F2) (X X) (X X) (F3 E4) | (X X) (X X) (X E5) (F4 X)
                row45_EF = Avx2.Shuffle(row45_EF, Avx.LoadVector256(shuffleVectorsPtr + (13 * 32)).AsByte());

                // row45_GH - (X X) (G1 H0) (H1 G2) (X X) | (X X) (X X) (X X) (X G3)
                Vector256<byte> row45_GH = Avx2.Shuffle(row2345_GH, Avx.LoadVector256(shuffleVectorsPtr + (14 * 32)).AsByte());

                Vector256<byte> row45 = Avx2.Or(Avx2.Or(row45_AB, row45_CD), Avx2.Or(row45_EF, row45_GH));


                // row67 - H2  H3  G4  F5  E6  D7  E7  F6 | G5  H4  H5  G6  F7  G7  H6  H7

                // row67_CD - (X X) (X X) (X D7) (X X) | (X X) (X X) (X X) (X X)
                Vector256<byte> row67_CD = Avx2.Shuffle(row4567_CD, Avx.LoadVector256(shuffleVectorsPtr + (15 * 32)).AsByte());

                // row67_EF - (E6 E7) (F4 F5) (F6 F7) (X X) | (F6 F7) (X X) (X X) (X X)
                Vector256<byte> row67_EF = Avx2.PermuteVar8x32(EF.AsInt32(), AB23_CD45_EF67_cr_ln_shfmask).AsByte();
                // row67_EF - (X X) (X F5) (E6 X) (E7 F6) | (X X) (X X) (F7 X) (X X)
                row67_EF = Avx2.Shuffle(row67_EF, Avx.LoadVector256(shuffleVectorsPtr + (16 * 32)).AsByte());

                // row67_GH - (G4 G5) (H2 H3) (X X) (X X) | (G4 G5) (G6 G7) (H4 H5) (H6 H7)
                Vector256<byte> row67_GH = Avx2.PermuteVar8x32(GH.AsInt32(), EF45_GH67_cr_ln_shfmask).AsByte();
                // row67_GH - (H2 H3) (G4 X) (X X) (X X) | (G5 H4) (H5 G6) (X G7) (H6 H7)
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
