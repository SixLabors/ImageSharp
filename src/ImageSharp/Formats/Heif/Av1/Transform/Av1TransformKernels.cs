// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Shared sixteen-bit vector operations of the register-resident transform kernels, after libaom's
/// <c>av1_txfm_sse2.h</c> and <c>txfm_common_avx2.h</c> helpers.
/// </summary>
internal static class Av1TransformKernels
{
    /// <summary>
    /// Gets a value indicating whether the register-resident kernels are available on this machine.
    /// </summary>
    public static bool IsSupported => Vector128.IsHardwareAccelerated;

    /// <summary>
    /// Gets a value indicating whether the sixteen-lane register-resident kernels are available on this machine.
    /// </summary>
    public static bool IsWideSupported => Vector256.IsHardwareAccelerated;

    /// <summary>
    /// Creates the interleaved weight pair of <c>pair_set_w16_epi16</c> with one broadcast.
    /// </summary>
    /// <param name="weight0">The weight of the even lanes.</param>
    /// <param name="weight1">The weight of the odd lanes.</param>
    /// <returns>The interleaved weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> PairWide(short weight0, short weight1)
        => Vector256.Create((ushort)weight0 | (weight1 << 16)).AsInt16();

    /// <summary>
    /// Computes both outputs of sixteen sixteen-bit butterflies in place, as <c>btf_16_w16_avx2</c> does.
    /// </summary>
    /// <param name="weights0">The interleaved weights of the first output.</param>
    /// <param name="weights1">The interleaved weights of the second output.</param>
    /// <param name="in0">The first input and output.</param>
    /// <param name="in1">The second input and output.</param>
    /// <param name="rounding">The rounding offset for the widened products.</param>
    /// <param name="cosBit">The number of fractional bits in the weights.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ButterflyWide(
        Vector256<short> weights0,
        Vector256<short> weights1,
        ref Vector256<short> in0,
        ref Vector256<short> in1,
        Vector256<int> rounding,
        int cosBit)
    {
        Vector256<short> lower = Vector256_.UnpackLow(in0, in1);
        Vector256<short> upper = Vector256_.UnpackHigh(in0, in1);
        Vector256<int> u0 = (Vector256_.MultiplyAddAdjacent(lower, weights0) + rounding) >> cosBit;
        Vector256<int> u1 = (Vector256_.MultiplyAddAdjacent(upper, weights0) + rounding) >> cosBit;
        Vector256<int> v0 = (Vector256_.MultiplyAddAdjacent(lower, weights1) + rounding) >> cosBit;
        Vector256<int> v1 = (Vector256_.MultiplyAddAdjacent(upper, weights1) + rounding) >> cosBit;
        in0 = Vector256_.PackSignedSaturate(u0, u1);
        in1 = Vector256_.PackSignedSaturate(v0, v1);
    }

    /// <summary>
    /// Replaces two vectors with their saturating sum and difference, as <c>btf_16_adds_subs_avx2</c> does.
    /// </summary>
    /// <param name="in0">The first input, replaced by the sum.</param>
    /// <param name="in1">The second input, replaced by the difference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddSubtractWide(ref Vector256<short> in0, ref Vector256<short> in1)
    {
        Vector256<short> sum = Vector256.AddSaturate(in0, in1);
        in1 = Vector256.SubtractSaturate(in0, in1);
        in0 = sum;
    }

    /// <summary>
    /// Transposes eight vectors of two eight-by-eight sixteen-bit tiles in place, one tile per
    /// one-hundred-twenty-eight-bit lane, as <c>transpose2_8x8_avx2</c> does.
    /// </summary>
    /// <param name="r0">The first row.</param>
    /// <param name="r1">The second row.</param>
    /// <param name="r2">The third row.</param>
    /// <param name="r3">The fourth row.</param>
    /// <param name="r4">The fifth row.</param>
    /// <param name="r5">The sixth row.</param>
    /// <param name="r6">The seventh row.</param>
    /// <param name="r7">The eighth row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void TransposeTiles8x8(
        ref Vector256<short> r0,
        ref Vector256<short> r1,
        ref Vector256<short> r2,
        ref Vector256<short> r3,
        ref Vector256<short> r4,
        ref Vector256<short> r5,
        ref Vector256<short> r6,
        ref Vector256<short> r7)
    {
        Vector256<short> a0 = Vector256_.UnpackLow(r0, r1);
        Vector256<short> a1 = Vector256_.UnpackLow(r2, r3);
        Vector256<short> a2 = Vector256_.UnpackLow(r4, r5);
        Vector256<short> a3 = Vector256_.UnpackLow(r6, r7);
        Vector256<short> a4 = Vector256_.UnpackHigh(r0, r1);
        Vector256<short> a5 = Vector256_.UnpackHigh(r2, r3);
        Vector256<short> a6 = Vector256_.UnpackHigh(r4, r5);
        Vector256<short> a7 = Vector256_.UnpackHigh(r6, r7);

        Vector256<int> b0 = Vector256_.UnpackLow(a0.AsInt32(), a1.AsInt32());
        Vector256<int> b1 = Vector256_.UnpackLow(a2.AsInt32(), a3.AsInt32());
        Vector256<int> b2 = Vector256_.UnpackLow(a4.AsInt32(), a5.AsInt32());
        Vector256<int> b3 = Vector256_.UnpackLow(a6.AsInt32(), a7.AsInt32());
        Vector256<int> b4 = Vector256_.UnpackHigh(a0.AsInt32(), a1.AsInt32());
        Vector256<int> b5 = Vector256_.UnpackHigh(a2.AsInt32(), a3.AsInt32());
        Vector256<int> b6 = Vector256_.UnpackHigh(a4.AsInt32(), a5.AsInt32());
        Vector256<int> b7 = Vector256_.UnpackHigh(a6.AsInt32(), a7.AsInt32());

        r0 = Vector256_.UnpackLow(b0.AsInt64(), b1.AsInt64()).AsInt16();
        r1 = Vector256_.UnpackHigh(b0.AsInt64(), b1.AsInt64()).AsInt16();
        r2 = Vector256_.UnpackLow(b4.AsInt64(), b5.AsInt64()).AsInt16();
        r3 = Vector256_.UnpackHigh(b4.AsInt64(), b5.AsInt64()).AsInt16();
        r4 = Vector256_.UnpackLow(b2.AsInt64(), b3.AsInt64()).AsInt16();
        r5 = Vector256_.UnpackHigh(b2.AsInt64(), b3.AsInt64()).AsInt16();
        r6 = Vector256_.UnpackLow(b6.AsInt64(), b7.AsInt64()).AsInt16();
        r7 = Vector256_.UnpackHigh(b6.AsInt64(), b7.AsInt64()).AsInt16();
    }

    /// <summary>
    /// Transposes sixteen vectors of sixteen sixteen-bit values in place, as <c>transpose_16bit_16x16_avx2</c> does.
    /// </summary>
    /// <remarks>
    /// The rows regroup into eight vectors that pair the left halves of rows <c>i</c> and <c>i + 8</c> and eight
    /// that pair the right halves, so that the per-lane tile transpose delivers the columns directly.
    /// </remarks>
    /// <param name="r0">The first row.</param>
    /// <param name="r1">The second row.</param>
    /// <param name="r2">The third row.</param>
    /// <param name="r3">The fourth row.</param>
    /// <param name="r4">The fifth row.</param>
    /// <param name="r5">The sixth row.</param>
    /// <param name="r6">The seventh row.</param>
    /// <param name="r7">The eighth row.</param>
    /// <param name="r8">The ninth row.</param>
    /// <param name="r9">The tenth row.</param>
    /// <param name="r10">The eleventh row.</param>
    /// <param name="r11">The twelfth row.</param>
    /// <param name="r12">The thirteenth row.</param>
    /// <param name="r13">The fourteenth row.</param>
    /// <param name="r14">The fifteenth row.</param>
    /// <param name="r15">The sixteenth row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose16x16(
        ref Vector256<short> r0,
        ref Vector256<short> r1,
        ref Vector256<short> r2,
        ref Vector256<short> r3,
        ref Vector256<short> r4,
        ref Vector256<short> r5,
        ref Vector256<short> r6,
        ref Vector256<short> r7,
        ref Vector256<short> r8,
        ref Vector256<short> r9,
        ref Vector256<short> r10,
        ref Vector256<short> r11,
        ref Vector256<short> r12,
        ref Vector256<short> r13,
        ref Vector256<short> r14,
        ref Vector256<short> r15)
    {
        Vector256<short> l0 = Vector256.Create(r0.GetLower(), r8.GetLower());
        Vector256<short> l1 = Vector256.Create(r1.GetLower(), r9.GetLower());
        Vector256<short> l2 = Vector256.Create(r2.GetLower(), r10.GetLower());
        Vector256<short> l3 = Vector256.Create(r3.GetLower(), r11.GetLower());
        Vector256<short> l4 = Vector256.Create(r4.GetLower(), r12.GetLower());
        Vector256<short> l5 = Vector256.Create(r5.GetLower(), r13.GetLower());
        Vector256<short> l6 = Vector256.Create(r6.GetLower(), r14.GetLower());
        Vector256<short> l7 = Vector256.Create(r7.GetLower(), r15.GetLower());
        Vector256<short> u0 = Vector256.Create(r0.GetUpper(), r8.GetUpper());
        Vector256<short> u1 = Vector256.Create(r1.GetUpper(), r9.GetUpper());
        Vector256<short> u2 = Vector256.Create(r2.GetUpper(), r10.GetUpper());
        Vector256<short> u3 = Vector256.Create(r3.GetUpper(), r11.GetUpper());
        Vector256<short> u4 = Vector256.Create(r4.GetUpper(), r12.GetUpper());
        Vector256<short> u5 = Vector256.Create(r5.GetUpper(), r13.GetUpper());
        Vector256<short> u6 = Vector256.Create(r6.GetUpper(), r14.GetUpper());
        Vector256<short> u7 = Vector256.Create(r7.GetUpper(), r15.GetUpper());

        TransposeTiles8x8(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7);
        TransposeTiles8x8(ref u0, ref u1, ref u2, ref u3, ref u4, ref u5, ref u6, ref u7);

        r0 = l0;
        r1 = l1;
        r2 = l2;
        r3 = l3;
        r4 = l4;
        r5 = l5;
        r6 = l6;
        r7 = l7;
        r8 = u0;
        r9 = u1;
        r10 = u2;
        r11 = u3;
        r12 = u4;
        r13 = u5;
        r14 = u6;
        r15 = u7;
    }

    /// <summary>
    /// Transposes sixteen consecutive vectors of sixteen sixteen-bit values into sixteen consecutive vectors, as
    /// <c>transpose_16bit_16x16_avx2</c> does with separate input and output buffers.
    /// </summary>
    /// <remarks>
    /// All sixteen rows are read before any output is written, so the source and the destination may be the same.
    /// </remarks>
    /// <param name="source">The first of the sixteen rows.</param>
    /// <param name="destination">The first of the sixteen transposed rows.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose16x16(ref Vector256<short> source, ref Vector256<short> destination)
    {
        Vector256<short> r0 = source;
        Vector256<short> r1 = Unsafe.Add(ref source, (nuint)1);
        Vector256<short> r2 = Unsafe.Add(ref source, (nuint)2);
        Vector256<short> r3 = Unsafe.Add(ref source, (nuint)3);
        Vector256<short> r4 = Unsafe.Add(ref source, (nuint)4);
        Vector256<short> r5 = Unsafe.Add(ref source, (nuint)5);
        Vector256<short> r6 = Unsafe.Add(ref source, (nuint)6);
        Vector256<short> r7 = Unsafe.Add(ref source, (nuint)7);
        Vector256<short> r8 = Unsafe.Add(ref source, (nuint)8);
        Vector256<short> r9 = Unsafe.Add(ref source, (nuint)9);
        Vector256<short> r10 = Unsafe.Add(ref source, (nuint)10);
        Vector256<short> r11 = Unsafe.Add(ref source, (nuint)11);
        Vector256<short> r12 = Unsafe.Add(ref source, (nuint)12);
        Vector256<short> r13 = Unsafe.Add(ref source, (nuint)13);
        Vector256<short> r14 = Unsafe.Add(ref source, (nuint)14);
        Vector256<short> r15 = Unsafe.Add(ref source, (nuint)15);

        Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);

        destination = r0;
        Unsafe.Add(ref destination, (nuint)1) = r1;
        Unsafe.Add(ref destination, (nuint)2) = r2;
        Unsafe.Add(ref destination, (nuint)3) = r3;
        Unsafe.Add(ref destination, (nuint)4) = r4;
        Unsafe.Add(ref destination, (nuint)5) = r5;
        Unsafe.Add(ref destination, (nuint)6) = r6;
        Unsafe.Add(ref destination, (nuint)7) = r7;
        Unsafe.Add(ref destination, (nuint)8) = r8;
        Unsafe.Add(ref destination, (nuint)9) = r9;
        Unsafe.Add(ref destination, (nuint)10) = r10;
        Unsafe.Add(ref destination, (nuint)11) = r11;
        Unsafe.Add(ref destination, (nuint)12) = r12;
        Unsafe.Add(ref destination, (nuint)13) = r13;
        Unsafe.Add(ref destination, (nuint)14) = r14;
        Unsafe.Add(ref destination, (nuint)15) = r15;
    }

    /// <summary>
    /// Scales one vector by two rounded fixed-point weights, as <c>btf_16_w16_0_avx2</c> does.
    /// </summary>
    /// <param name="weight0">The first weight, with twelve fractional bits.</param>
    /// <param name="weight1">The second weight, with twelve fractional bits.</param>
    /// <param name="input">The input.</param>
    /// <param name="output0">The input times the first weight.</param>
    /// <param name="output1">The input times the second weight.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ScaleWide(short weight0, short weight1, Vector256<short> input, out Vector256<short> output0, out Vector256<short> output1)
    {
        output0 = Vector256_.MultiplyHighRoundScale(input, Vector256.Create((short)(weight0 * 8)));
        output1 = Vector256_.MultiplyHighRoundScale(input, Vector256.Create((short)(weight1 * 8)));
    }

    /// <summary>
    /// Rounds and shifts right by a bit count through a rounding multiply, as the inverse drivers' <c>_mm256_mulhrs_epi16</c>
    /// scaling does.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">The number of bits to shift right.</param>
    /// <returns>The rounded value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> RoundShiftRightWide(Vector256<short> value, int bits)
        => Vector256_.MultiplyHighRoundScale(value, Vector256.Create((short)(1 << (15 - bits))));

    /// <summary>
    /// Loads sixteen thirty-two-bit coefficients and packs them to sixteen bits with saturation, as
    /// <c>load_32bit_to_16bit_w16_avx2</c> does.
    /// </summary>
    /// <param name="source">The coefficient base.</param>
    /// <param name="offset">The offset in coefficients.</param>
    /// <returns>The packed coefficients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> Load16(ref int source, int offset)
    {
        Vector256<short> packed = Vector256_.PackSignedSaturate(
            Vector256.LoadUnsafe(ref source, (nuint)offset),
            Vector256.LoadUnsafe(ref source, (nuint)(offset + 8)));

        return Vector256_.Permute4x64(packed.AsInt64(), 0xD8).AsInt16();
    }

    /// <summary>
    /// Adds sixteen residuals to sixteen predicted samples with clipping, as <c>write_recon_w16_avx2</c> does.
    /// </summary>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="residual">The residuals.</param>
    /// <param name="destination">The reconstructed samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddClip16(ref byte prediction, Vector256<short> residual, ref byte destination)
    {
        Vector256<short> sum = Vector256.AddSaturate(Vector256_.Widen(Vector128.LoadUnsafe(ref prediction)), residual);
        Vector128_.PackUnsignedSaturate(sum.GetLower(), sum.GetUpper()).StoreUnsafe(ref destination);
    }

    /// <summary>
    /// Widens sixteen sixteen-bit coefficients to thirty-two bits and stores them, as
    /// <c>store_buffer_16bit_to_32bit_w16_avx2</c> does for one row.
    /// </summary>
    /// <param name="values">The coefficients.</param>
    /// <param name="destination">The destination base.</param>
    /// <param name="offset">The destination offset in coefficients.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store16(Vector256<short> values, ref int destination, int offset)
    {
        Vector256_.Widen(values.GetLower()).StoreUnsafe(ref destination, (nuint)offset);
        Vector256_.Widen(values.GetUpper()).StoreUnsafe(ref destination, (nuint)(offset + 8));
    }

    /// <summary>
    /// Creates the interleaved weight pair of <c>pair_set_epi16</c> with one broadcast.
    /// </summary>
    /// <param name="weight0">The weight of the even lanes.</param>
    /// <param name="weight1">The weight of the odd lanes.</param>
    /// <returns>The interleaved weights.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> Pair(short weight0, short weight1)
        => Vector128.Create((ushort)weight0 | (weight1 << 16)).AsInt16();

    /// <summary>
    /// Computes both outputs of eight sixteen-bit butterflies, as <c>btf_16_sse2</c> does.
    /// </summary>
    /// <param name="weights0">The interleaved weights of the first output.</param>
    /// <param name="weights1">The interleaved weights of the second output.</param>
    /// <param name="input0">The first input.</param>
    /// <param name="input1">The second input.</param>
    /// <param name="output0">The first output.</param>
    /// <param name="output1">The second output.</param>
    /// <param name="cosBit">The number of fractional bits in the weights.</param>
    /// <param name="rounding">The rounding offset for the widened products.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Butterfly16(
        Vector128<short> weights0,
        Vector128<short> weights1,
        Vector128<short> input0,
        Vector128<short> input1,
        out Vector128<short> output0,
        out Vector128<short> output1,
        int cosBit,
        Vector128<int> rounding)
    {
        Vector128<short> lower = Vector128_.UnpackLow(input0, input1);
        Vector128<short> upper = Vector128_.UnpackHigh(input0, input1);
        Vector128<int> u0 = (Vector128_.MultiplyAddAdjacent(lower, weights0) + rounding) >> cosBit;
        Vector128<int> u1 = (Vector128_.MultiplyAddAdjacent(upper, weights0) + rounding) >> cosBit;
        Vector128<int> v0 = (Vector128_.MultiplyAddAdjacent(lower, weights1) + rounding) >> cosBit;
        Vector128<int> v1 = (Vector128_.MultiplyAddAdjacent(upper, weights1) + rounding) >> cosBit;
        output0 = Vector128_.PackSignedSaturate(u0, u1);
        output1 = Vector128_.PackSignedSaturate(v0, v1);
    }

    /// <summary>
    /// Scales one vector by two rounded fixed-point weights, as <c>btf_16_ssse3</c> does.
    /// </summary>
    /// <param name="weight0">The first weight, with twelve fractional bits.</param>
    /// <param name="weight1">The second weight, with twelve fractional bits.</param>
    /// <param name="input">The input.</param>
    /// <param name="output0">The input times the first weight.</param>
    /// <param name="output1">The input times the second weight.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Scale16(short weight0, short weight1, Vector128<short> input, out Vector128<short> output0, out Vector128<short> output1)
    {
        output0 = Vector128_.MultiplyHighRoundScale(input, Vector128.Create((short)(weight0 * 8)));
        output1 = Vector128_.MultiplyHighRoundScale(input, Vector128.Create((short)(weight1 * 8)));
    }

    /// <summary>
    /// Rounds and shifts right by a negative bit count, as <c>round_shift_16bit_ssse3</c> does.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">The number of bits to shift right.</param>
    /// <returns>The rounded value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> RoundShiftRight(Vector128<short> value, int bits)
        => Vector128_.MultiplyHighRoundScale(value, Vector128.Create((short)(1 << (15 - bits))));

    /// <summary>
    /// Transposes the low four lanes of four vectors in place, as <c>transpose_16bit_4x4</c> does.
    /// </summary>
    /// <param name="r0">The first row.</param>
    /// <param name="r1">The second row.</param>
    /// <param name="r2">The third row.</param>
    /// <param name="r3">The fourth row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose4x4(
        ref Vector128<short> r0,
        ref Vector128<short> r1,
        ref Vector128<short> r2,
        ref Vector128<short> r3)
    {
        Vector128<short> a0 = Vector128_.UnpackLow(r0, r1);
        Vector128<short> a1 = Vector128_.UnpackLow(r2, r3);
        Vector128<int> b0 = Vector128_.UnpackLow(a0.AsInt32(), a1.AsInt32());
        Vector128<int> b2 = Vector128_.UnpackHigh(a0.AsInt32(), a1.AsInt32());
        r0 = b0.AsInt16();
        r1 = Vector128_.ShiftRightBytesInVector(b0.AsByte(), 8).AsInt32().AsInt16();
        r2 = b2.AsInt16();
        r3 = Vector128_.ShiftRightBytesInVector(b2.AsByte(), 8).AsInt32().AsInt16();
    }

    /// <summary>
    /// Transposes eight vectors of eight sixteen-bit values in place, as <c>transpose_16bit_8x8</c> does.
    /// </summary>
    /// <param name="r0">The first row.</param>
    /// <param name="r1">The second row.</param>
    /// <param name="r2">The third row.</param>
    /// <param name="r3">The fourth row.</param>
    /// <param name="r4">The fifth row.</param>
    /// <param name="r5">The sixth row.</param>
    /// <param name="r6">The seventh row.</param>
    /// <param name="r7">The eighth row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose8x8(
        ref Vector128<short> r0,
        ref Vector128<short> r1,
        ref Vector128<short> r2,
        ref Vector128<short> r3,
        ref Vector128<short> r4,
        ref Vector128<short> r5,
        ref Vector128<short> r6,
        ref Vector128<short> r7)
    {
        Vector128<short> a0 = Vector128_.UnpackLow(r0, r1);
        Vector128<short> a1 = Vector128_.UnpackLow(r2, r3);
        Vector128<short> a2 = Vector128_.UnpackLow(r4, r5);
        Vector128<short> a3 = Vector128_.UnpackLow(r6, r7);
        Vector128<short> a4 = Vector128_.UnpackHigh(r0, r1);
        Vector128<short> a5 = Vector128_.UnpackHigh(r2, r3);
        Vector128<short> a6 = Vector128_.UnpackHigh(r4, r5);
        Vector128<short> a7 = Vector128_.UnpackHigh(r6, r7);

        Vector128<int> b0 = Vector128_.UnpackLow(a0.AsInt32(), a1.AsInt32());
        Vector128<int> b1 = Vector128_.UnpackLow(a2.AsInt32(), a3.AsInt32());
        Vector128<int> b2 = Vector128_.UnpackLow(a4.AsInt32(), a5.AsInt32());
        Vector128<int> b3 = Vector128_.UnpackLow(a6.AsInt32(), a7.AsInt32());
        Vector128<int> b4 = Vector128_.UnpackHigh(a0.AsInt32(), a1.AsInt32());
        Vector128<int> b5 = Vector128_.UnpackHigh(a2.AsInt32(), a3.AsInt32());
        Vector128<int> b6 = Vector128_.UnpackHigh(a4.AsInt32(), a5.AsInt32());
        Vector128<int> b7 = Vector128_.UnpackHigh(a6.AsInt32(), a7.AsInt32());

        r0 = Vector128_.UnpackLow(b0.AsInt64(), b1.AsInt64()).AsInt16();
        r1 = Vector128_.UnpackHigh(b0.AsInt64(), b1.AsInt64()).AsInt16();
        r2 = Vector128_.UnpackLow(b4.AsInt64(), b5.AsInt64()).AsInt16();
        r3 = Vector128_.UnpackHigh(b4.AsInt64(), b5.AsInt64()).AsInt16();
        r4 = Vector128_.UnpackLow(b2.AsInt64(), b3.AsInt64()).AsInt16();
        r5 = Vector128_.UnpackHigh(b2.AsInt64(), b3.AsInt64()).AsInt16();
        r6 = Vector128_.UnpackLow(b6.AsInt64(), b7.AsInt64()).AsInt16();
        r7 = Vector128_.UnpackHigh(b6.AsInt64(), b7.AsInt64()).AsInt16();
    }

    /// <summary>
    /// Widens eight sixteen-bit coefficients to thirty-two bits and stores them, as <c>store_16bit_to_32bit</c> does.
    /// </summary>
    /// <param name="values">The coefficients.</param>
    /// <param name="destination">The destination base.</param>
    /// <param name="offset">The destination offset in coefficients.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store8(Vector128<short> values, ref int destination, int offset)
    {
        Vector128<int> lower = Vector128.ShiftRightArithmetic(Vector128_.UnpackLow(values, values).AsInt32(), 16);
        Vector128<int> upper = Vector128.ShiftRightArithmetic(Vector128_.UnpackHigh(values, values).AsInt32(), 16);
        lower.StoreUnsafe(ref destination, (nuint)offset);
        upper.StoreUnsafe(ref destination, (nuint)(offset + 4));
    }

    /// <summary>
    /// Loads eight thirty-two-bit coefficients and packs them to sixteen bits with saturation, as
    /// <c>load_32bit_to_16bit</c> does.
    /// </summary>
    /// <param name="source">The coefficient base.</param>
    /// <param name="offset">The offset in coefficients.</param>
    /// <returns>The packed coefficients.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> Load8(ref int source, int offset)
        => Vector128_.PackSignedSaturate(Vector128.LoadUnsafe(ref source, (nuint)offset), Vector128.LoadUnsafe(ref source, (nuint)(offset + 4)));

    /// <summary>
    /// Adds eight residuals to eight predicted samples with clipping, as <c>lowbd_get_recon_8x8_sse2</c> does.
    /// </summary>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="residual">The residuals.</param>
    /// <param name="destination">The reconstructed samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddClip8(ref byte prediction, Vector128<short> residual, ref byte destination)
    {
        Vector128<byte> predicted = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref prediction)).AsByte();
        Vector128<short> sum = Vector128.AddSaturate(residual, Vector128_.UnpackLow(predicted, Vector128<byte>.Zero).AsInt16());
        Unsafe.WriteUnaligned(ref destination, Vector128_.PackUnsignedSaturate(sum, sum).AsUInt64().ToScalar());
    }
}
