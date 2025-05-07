// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// <see cref="Vector128{Single}"/> version of <see cref="Block8x8F"/>.
/// </content>
internal partial struct Block8x8F
{
    /// <summary>
    /// A number of rows of 8 scalar coefficients each in <see cref="Block8x8F"/>
    /// </summary>
    public const int RowCount = 8;

#pragma warning disable SA1310 // Field names should not contain underscore
    [FieldOffset(0)]
    public Vector256<float> V256_0;
    [FieldOffset(32)]
    public Vector256<float> V256_1;
    [FieldOffset(64)]
    public Vector256<float> V256_2;
    [FieldOffset(96)]
    public Vector256<float> V256_3;
    [FieldOffset(128)]
    public Vector256<float> V256_4;
    [FieldOffset(160)]
    public Vector256<float> V256_5;
    [FieldOffset(192)]
    public Vector256<float> V256_6;
    [FieldOffset(224)]
    public Vector256<float> V256_7;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>
    /// <see cref="Vector256{Single}"/> version of <see cref="NormalizeColorsInPlace(float)"/> and <see cref="RoundInPlace()"/>.
    /// </summary>
    /// <param name="maximum">The maximum value to normalize to.</param>
    [MethodImpl(InliningOptions.ShortMethod)]
    public void NormalizeColorsAndRoundInPlaceVector256(float maximum)
    {
        Vector256<float> off = Vector256.Create(MathF.Ceiling(maximum * 0.5F));
        Vector256<float> max = Vector256.Create(maximum);

        this.V256_0 = NormalizeAndRoundVector256(this.V256_0, off, max);
        this.V256_1 = NormalizeAndRoundVector256(this.V256_1, off, max);
        this.V256_2 = NormalizeAndRoundVector256(this.V256_2, off, max);
        this.V256_3 = NormalizeAndRoundVector256(this.V256_3, off, max);
        this.V256_4 = NormalizeAndRoundVector256(this.V256_4, off, max);
        this.V256_5 = NormalizeAndRoundVector256(this.V256_5, off, max);
        this.V256_6 = NormalizeAndRoundVector256(this.V256_6, off, max);
        this.V256_7 = NormalizeAndRoundVector256(this.V256_7, off, max);
    }

    /// <summary>
    /// Loads values from <paramref name="source"/> using extended AVX2 intrinsics.
    /// </summary>
    /// <param name="source">The source <see cref="Block8x8"/></param>
    public void LoadFromInt16ExtendedAvx2(ref Block8x8 source)
    {
        DebugGuard.IsTrue(
            Avx2.IsSupported,
            "LoadFromUInt16ExtendedAvx2 only works on AVX2 compatible architecture!");

        ref short sRef = ref Unsafe.As<Block8x8, short>(ref source);
        ref Vector256<float> dRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref this);

        // Vector256<ushort>.Count == 16 on AVX2
        // We can process 2 block rows in a single step
        Vector256<int> top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef));
        Vector256<int> bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)Vector256<int>.Count));
        dRef = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 1) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 2)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 3)));
        Unsafe.Add(ref dRef, 2) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 3) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 4)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 5)));
        Unsafe.Add(ref dRef, 4) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 5) = Avx.ConvertToVector256Single(bottom);

        top = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 6)));
        bottom = Avx2.ConvertToVector256Int32(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 7)));
        Unsafe.Add(ref dRef, 6) = Avx.ConvertToVector256Single(top);
        Unsafe.Add(ref dRef, 7) = Avx.ConvertToVector256Single(bottom);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector256<float> NormalizeAndRoundVector256(Vector256<float> value, Vector256<float> off, Vector256<float> max)
        => Vector256_.RoundToNearestInteger(Vector256_.Clamp(value + off, Vector256<float>.Zero, max));

    private static unsafe void MultiplyIntoInt16_Avx2(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Avx2.IsSupported, "Avx2 support is required to run this operation!");

        ref Vector256<float> aBase = ref a.V256_0;
        ref Vector256<float> bBase = ref b.V256_0;

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

    private void TransposeInplace_Avx()
    {
        // https://stackoverflow.com/questions/25622745/transpose-an-8x8-float-using-avx-avx2/25627536#25627536
        Vector256<float> r0 = Avx.InsertVector128(
            this.V256_0,
            Unsafe.As<Vector4, Vector128<float>>(ref this.V4L),
            1);

        Vector256<float> r1 = Avx.InsertVector128(
           this.V256_1,
           Unsafe.As<Vector4, Vector128<float>>(ref this.V5L),
           1);

        Vector256<float> r2 = Avx.InsertVector128(
           this.V256_2,
           Unsafe.As<Vector4, Vector128<float>>(ref this.V6L),
           1);

        Vector256<float> r3 = Avx.InsertVector128(
           this.V256_3,
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
        this.V256_0 = Avx.Blend(t0, v, 0xCC);
        this.V256_1 = Avx.Blend(t2, v, 0x33);

        Vector256<float> t4 = Avx.UnpackLow(r4, r5);
        Vector256<float> t6 = Avx.UnpackLow(r6, r7);
        v = Avx.Shuffle(t4, t6, 0x4E);
        this.V256_4 = Avx.Blend(t4, v, 0xCC);
        this.V256_5 = Avx.Blend(t6, v, 0x33);

        Vector256<float> t1 = Avx.UnpackHigh(r0, r1);
        Vector256<float> t3 = Avx.UnpackHigh(r2, r3);
        v = Avx.Shuffle(t1, t3, 0x4E);
        this.V256_2 = Avx.Blend(t1, v, 0xCC);
        this.V256_3 = Avx.Blend(t3, v, 0x33);

        Vector256<float> t5 = Avx.UnpackHigh(r4, r5);
        Vector256<float> t7 = Avx.UnpackHigh(r6, r7);
        v = Avx.Shuffle(t5, t7, 0x4E);
        this.V256_6 = Avx.Blend(t5, v, 0xCC);
        this.V256_7 = Avx.Blend(t7, v, 0x33);
    }
}
