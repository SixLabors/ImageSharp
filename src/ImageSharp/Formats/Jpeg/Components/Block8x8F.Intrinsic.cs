// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

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

    private static unsafe void MultiplyIntoInt16_Avx2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Avx2.IsSupported, "Avx2 support is required to run this operation!");

        ref Vector256<float> aBase = ref a.V0;
        ref Vector256<float> bBase = ref b.V0;

        ref Vector256<short> destRef = ref dest.V01;
        Vector256<int> multiplyIntoInt16ShuffleMask = Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7);

        for (nuint i = 0; i < 8; i += 2)
        {
            Vector256<int> row0 = Avx.ConvertToVector256Int32(Avx.Multiply(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
            Vector256<int> row1 = Avx.ConvertToVector256Int32(Avx.Multiply(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

            Vector256<short> row = Avx2.PackSignedSaturate(row0, row1);
            row = Avx2.PermuteVar8x32(row.AsInt32(), multiplyIntoInt16ShuffleMask).AsInt16();

            Unsafe.Add(ref destRef, i / 2) = row;
        }
    }

    private static void MultiplyIntoInt16_Sse2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Sse2.IsSupported, "Sse2 support is required to run this operation!");

        ref Vector128<float> aBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref a);
        ref Vector128<float> bBase = ref Unsafe.As<Block8x8F, Vector128<float>>(ref b);

        ref Vector128<short> destBase = ref Unsafe.As<Block8x8, Vector128<short>>(ref dest);

        // TODO: We can use the v128 utilities for this.
        for (nuint i = 0; i < 16; i += 2)
        {
            Vector128<int> left = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 0), Unsafe.Add(ref bBase, i + 0)));
            Vector128<int> right = Sse2.ConvertToVector128Int32(Sse.Multiply(Unsafe.Add(ref aBase, i + 1), Unsafe.Add(ref bBase, i + 1)));

            Vector128<short> row = Sse2.PackSignedSaturate(left, right);
            Unsafe.Add(ref destBase, i / 2) = row;
        }
    }

    private void TransposeInplace_Avx()
    {
        // https://stackoverflow.com/questions/25622745/transpose-an-8x8-float-using-avx-avx2/25627536#25627536
        Vector256<float> r0 = Avx.InsertVector128(
            this.V0,
            Unsafe.As<Vector4, Vector128<float>>(ref this.V4L),
            1);

        Vector256<float> r1 = Avx.InsertVector128(
           this.V1,
           Unsafe.As<Vector4, Vector128<float>>(ref this.V5L),
           1);

        Vector256<float> r2 = Avx.InsertVector128(
           this.V2,
           Unsafe.As<Vector4, Vector128<float>>(ref this.V6L),
           1);

        Vector256<float> r3 = Avx.InsertVector128(
           this.V3,
           Unsafe.As<Vector4, Vector128<float>>(ref this.V7L),
           1);

        Vector256<float> r4 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref this.V0R).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref this.V4R),
           1);

        Vector256<float> r5 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref this.V1R).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref this.V5R),
           1);

        Vector256<float> r6 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref this.V2R).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref this.V6R),
           1);

        Vector256<float> r7 = Avx.InsertVector128(
           Unsafe.As<Vector4, Vector128<float>>(ref this.V3R).ToVector256(),
           Unsafe.As<Vector4, Vector128<float>>(ref this.V7R),
           1);

        Vector256<float> t0 = Avx.UnpackLow(r0, r1);
        Vector256<float> t2 = Avx.UnpackLow(r2, r3);
        Vector256<float> v = Avx.Shuffle(t0, t2, 0x4E);
        this.V0 = Avx.Blend(t0, v, 0xCC);
        this.V1 = Avx.Blend(t2, v, 0x33);

        Vector256<float> t4 = Avx.UnpackLow(r4, r5);
        Vector256<float> t6 = Avx.UnpackLow(r6, r7);
        v = Avx.Shuffle(t4, t6, 0x4E);
        this.V4 = Avx.Blend(t4, v, 0xCC);
        this.V5 = Avx.Blend(t6, v, 0x33);

        Vector256<float> t1 = Avx.UnpackHigh(r0, r1);
        Vector256<float> t3 = Avx.UnpackHigh(r2, r3);
        v = Avx.Shuffle(t1, t3, 0x4E);
        this.V2 = Avx.Blend(t1, v, 0xCC);
        this.V3 = Avx.Blend(t3, v, 0x33);

        Vector256<float> t5 = Avx.UnpackHigh(r4, r5);
        Vector256<float> t7 = Avx.UnpackHigh(r6, r7);
        v = Avx.Shuffle(t5, t7, 0x4E);
        this.V6 = Avx.Blend(t5, v, 0xCC);
        this.V7 = Avx.Blend(t7, v, 0x33);
    }
}
