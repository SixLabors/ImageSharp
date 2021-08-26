// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if SUPPORTS_RUNTIME_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components
{
    internal partial struct Block8x8F
    {
        /// <summary>
        /// A number of rows of 8 scalar coefficients each in <see cref="Block8x8F"/>
        /// </summary>
        public const int RowCount = 8;

        [FieldOffset(0)]
        public Vector256<float> V0;
        [FieldOffset(32)]
        public Vector256<float> V1;
        [FieldOffset(64)]
        public Vector256<float> V2;
        [FieldOffset(96)]
        public Vector256<float> V3;
        [FieldOffset(128)]
        public Vector256<float> V4;
        [FieldOffset(160)]
        public Vector256<float> V5;
        [FieldOffset(192)]
        public Vector256<float> V6;
        [FieldOffset(224)]
        public Vector256<float> V7;

        private static ReadOnlySpan<int> DivideIntoInt16_Avx2_ShuffleMask => new int[] {
            0, 1, 4, 5, 2, 3, 6, 7
        };

        private static unsafe void DivideIntoInt16_Avx2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
        {
            DebugGuard.IsTrue(Avx2.IsSupported, "Avx2 support is required to run this operation!");

            fixed (int* maskPtr = DivideIntoInt16_Avx2_ShuffleMask)
            {
                Vector256<int> crossLaneShuffleMask = Avx.LoadVector256(maskPtr).AsInt32();

                ref Vector256<float> aBase = ref Unsafe.As<Block8x8F, Vector256<float>>(ref a);
                ref Vector256<float> bBase = ref Unsafe.As<Block8x8F, Vector256<float>>(ref b);

                ref Vector256<short> destBase = ref Unsafe.As<Block8x8, Vector256<short>>(ref dest);

                for (int i = 0; i < 8; i += 2)
                {
                    Vector256<int> row0 = Avx.ConvertToVector256Int32(Avx.Divide(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
                    Vector256<int> row1 = Avx.ConvertToVector256Int32(Avx.Divide(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

                    Vector256<short> row = Avx2.PackSignedSaturate(row0, row1);
                    row = Avx2.PermuteVar8x32(row.AsInt32(), crossLaneShuffleMask).AsInt16();

                    Unsafe.Add(ref destBase, i / 2) = row;
                }
            }
        }

        private static void DivideIntoInt16_Sse2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
        {
            DebugGuard.IsTrue(Sse2.IsSupported, "Sse2 support is required to run this operation!");

            ref Vector128<float> aBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref a);
            ref Vector128<float> bBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref b);

            ref Vector128<short> destBase = ref Unsafe.As<Block8x8, Vector128<short>>(ref dest);

            for (int i = 0; i < 16; i += 2)
            {
                Vector128<int> left = Sse2.ConvertToVector128Int32(Sse.Divide(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
                Vector128<int> right = Sse2.ConvertToVector128Int32(Sse.Divide(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

                Vector128<short> row = Sse2.PackSignedSaturate(left, right);
                Unsafe.Add(ref destBase, i / 2) = row;
            }
        }
    }
}
#endif
