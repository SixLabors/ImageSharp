// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
        Vector256<float> max = Vector256.Create(maximum);
        Vector256<float> off = Vector256.Ceiling(max * .5F);

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
    /// Loads values from <paramref name="source"/> using <see cref="Vector256{T}"/> intrinsics.
    /// </summary>
    /// <param name="source">The source <see cref="Block8x8"/></param>
    public void LoadFromInt16ExtendedVector256(ref Block8x8 source)
    {
        DebugGuard.IsTrue(
            Vector256.IsHardwareAccelerated,
            "LoadFromInt16ExtendedVector256 only works on Vector256 compatible architecture!");

        ref short sRef = ref Unsafe.As<Block8x8, short>(ref source);
        ref Vector256<float> dRef = ref Unsafe.As<Block8x8F, Vector256<float>>(ref this);

        // Vector256<ushort>.Count == 16
        // We can process 2 block rows in a single step
        Vector256<int> top = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef));
        Vector256<int> bottom = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)Vector256<int>.Count));
        dRef = Vector256.ConvertToSingle(top);
        Unsafe.Add(ref dRef, 1) = Vector256.ConvertToSingle(bottom);

        top = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 2)));
        bottom = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 3)));
        Unsafe.Add(ref dRef, 2) = Vector256.ConvertToSingle(top);
        Unsafe.Add(ref dRef, 3) = Vector256.ConvertToSingle(bottom);

        top = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 4)));
        bottom = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 5)));
        Unsafe.Add(ref dRef, 4) = Vector256.ConvertToSingle(top);
        Unsafe.Add(ref dRef, 5) = Vector256.ConvertToSingle(bottom);

        top = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 6)));
        bottom = Vector256_.Widen(Vector128.LoadUnsafe(ref sRef, (nuint)(Vector256<int>.Count * 7)));
        Unsafe.Add(ref dRef, 6) = Vector256.ConvertToSingle(top);
        Unsafe.Add(ref dRef, 7) = Vector256.ConvertToSingle(bottom);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static Vector256<float> NormalizeAndRoundVector256(Vector256<float> value, Vector256<float> off, Vector256<float> max)
        => Vector256_.RoundToNearestInteger(Vector256_.Clamp(value + off, Vector256<float>.Zero, max));

    private static unsafe void MultiplyIntoInt16Vector256(ref Block8x8F a, ref Block8x8F b, ref Block8x8 dest)
    {
        DebugGuard.IsTrue(Vector256.IsHardwareAccelerated, "Vector256 support is required to run this operation!");

        ref Vector256<float> aBase = ref a.V256_0;
        ref Vector256<float> bBase = ref b.V256_0;
        ref Vector256<short> destRef = ref dest.V01;

        for (nuint i = 0; i < 8; i += 2)
        {
            Vector256<int> row0 = Vector256_.ConvertToInt32RoundToEven(Unsafe.Add(ref aBase, i + 0) * Unsafe.Add(ref bBase, i + 0));
            Vector256<int> row1 = Vector256_.ConvertToInt32RoundToEven(Unsafe.Add(ref aBase, i + 1) * Unsafe.Add(ref bBase, i + 1));

            Vector256<short> row = Vector256_.PackSignedSaturate(row0, row1);
            row = Vector256.Shuffle(row.AsInt32(), Vector256.Create(0, 1, 4, 5, 2, 3, 6, 7)).AsInt16();

            Unsafe.Add(ref destRef, i / 2) = row;
        }
    }

    private void TransposeInPlaceVector256()
    {
        // https://stackoverflow.com/questions/25622745/transpose-an-8x8-float-using-avx-avx2/25627536#25627536
        Vector256<float> r0 = this.V256_0.WithUpper(this.V4L.AsVector128());
        Vector256<float> r1 = this.V256_1.WithUpper(this.V5L.AsVector128());
        Vector256<float> r2 = this.V256_2.WithUpper(this.V6L.AsVector128());
        Vector256<float> r3 = this.V256_3.WithUpper(this.V7L.AsVector128());
        Vector256<float> r4 = this.V0R.AsVector128().ToVector256().WithUpper(this.V4R.AsVector128());
        Vector256<float> r5 = this.V1R.AsVector128().ToVector256().WithUpper(this.V5R.AsVector128());
        Vector256<float> r6 = this.V2R.AsVector128().ToVector256().WithUpper(this.V6R.AsVector128());
        Vector256<float> r7 = this.V3R.AsVector128().ToVector256().WithUpper(this.V7R.AsVector128());

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
