// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident forward transform kernels for the four-by-eight and eight-by-four eight-bit block shapes.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Applies a four-wide, eight-high eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// The stage shifts are 2, -1 and 0, and both cosine bit counts are 13.
    /// The column transform is an eight-point kernel on vectors whose low four lanes hold the four columns.
    /// The row transform is a four-point kernel on vectors whose eight lanes hold the eight rows.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform4x8(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform4x8<Dct8Kernel, Dct4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform4x8<Adst8Kernel, Dct4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform4x8<Dct8Kernel, Adst4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform4x8<Adst8Kernel, Adst4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform4x8<Adst8Kernel, Dct4WideKernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform4x8<Dct8Kernel, Adst4WideKernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform4x8<Adst8Kernel, Adst4WideKernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform4x8<Adst8Kernel, Adst4WideKernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform4x8<Adst8Kernel, Adst4WideKernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform4x8<Identity8Kernel, Identity4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform4x8<Dct8Kernel, Identity4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform4x8<Identity8Kernel, Dct4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform4x8<Adst8Kernel, Identity4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform4x8<Identity8Kernel, Adst4WideKernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform4x8<Adst8Kernel, Identity4WideKernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform4x8<Identity8Kernel, Adst4WideKernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one eight-point column kernel and one four-point row kernel to a four-wide, eight-high block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform4x8<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel8
        where TRow : struct, IAv1Kernel4Wide
    {
        // Each of the eight rows loads into the low four lanes of one vector, and the first stage shift of 2 applies.
        // The column kernel works lane by lane, so the unused high lanes never reach a stored coefficient.
        ref short source = ref MemoryMarshal.GetReference(input);
        Vector128<short> r0;
        Vector128<short> r1;
        Vector128<short> r2;
        Vector128<short> r3;
        Vector128<short> r4;
        Vector128<short> r5;
        Vector128<short> r6;
        Vector128<short> r7;
        if (flipUpsideDown)
        {
            r7 = Load4(ref source, 0) << 2;
            r6 = Load4(ref source, stride) << 2;
            r5 = Load4(ref source, 2 * stride) << 2;
            r4 = Load4(ref source, 3 * stride) << 2;
            r3 = Load4(ref source, 4 * stride) << 2;
            r2 = Load4(ref source, 5 * stride) << 2;
            r1 = Load4(ref source, 6 * stride) << 2;
            r0 = Load4(ref source, 7 * stride) << 2;
        }
        else
        {
            r0 = Load4(ref source, 0) << 2;
            r1 = Load4(ref source, stride) << 2;
            r2 = Load4(ref source, 2 * stride) << 2;
            r3 = Load4(ref source, 3 * stride) << 2;
            r4 = Load4(ref source, 4 * stride) << 2;
            r5 = Load4(ref source, 5 * stride) << 2;
            r6 = Load4(ref source, 6 * stride) << 2;
            r7 = Load4(ref source, 7 * stride) << 2;
        }

        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, 13);

        // The second stage shift of -1 adds 1, half of the divisor 2, and then shifts right by 1. The addition saturates.
        Vector128<short> half = Vector128.Create((short)1);
        r0 = Vector128.AddSaturate(r0, half) >> 1;
        r1 = Vector128.AddSaturate(r1, half) >> 1;
        r2 = Vector128.AddSaturate(r2, half) >> 1;
        r3 = Vector128.AddSaturate(r3, half) >> 1;
        r4 = Vector128.AddSaturate(r4, half) >> 1;
        r5 = Vector128.AddSaturate(r5, half) >> 1;
        r6 = Vector128.AddSaturate(r6, half) >> 1;
        r7 = Vector128.AddSaturate(r7, half) >> 1;

        // After the transpose, vectors 0 to 3 each hold one column with the eight rows in its lanes. Vectors 4 to 7 hold only the unused high lanes.
        Av1TransformKernels.Transpose8x8(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);

        // The row kernel transforms across the four column vectors, lane by lane. A horizontal flip enters the columns in reverse order.
        // The outputs then hold one horizontal frequency each, with the eight vertical frequencies in their lanes.
        Vector128<short> zero = Vector128<short>.Zero;
        Vector128<short> f0;
        Vector128<short> f1;
        Vector128<short> f2;
        Vector128<short> f3;
        if (flipLeftToRight)
        {
            TRow.Transform(ref r3, ref r2, ref r1, ref r0, 13);
            (f0, f1, f2, f3) = (r3, r2, r1, r0);
        }
        else
        {
            TRow.Transform(ref r0, ref r1, ref r2, ref r3, 13);
            (f0, f1, f2, f3) = (r0, r1, r2, r3);
        }

        // The coefficients are row-major, vertical frequency first. One more transpose gives eight vectors with four horizontal frequencies each.
        // Zero vectors fill the unused inputs of the transpose. The third shift is zero.
        // The 2:1 shape scales each coefficient by the square root of two when the store widens it.
        Vector128<short> f4 = zero;
        Vector128<short> f5 = zero;
        Vector128<short> f6 = zero;
        Vector128<short> f7 = zero;
        Av1TransformKernels.Transpose8x8(ref f0, ref f1, ref f2, ref f3, ref f4, ref f5, ref f6, ref f7);
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        StoreRect4(f0, ref destination, 0);
        StoreRect4(f1, ref destination, 4);
        StoreRect4(f2, ref destination, 8);
        StoreRect4(f3, ref destination, 12);
        StoreRect4(f4, ref destination, 16);
        StoreRect4(f5, ref destination, 20);
        StoreRect4(f6, ref destination, 24);
        StoreRect4(f7, ref destination, 28);
    }

    /// <summary>
    /// Applies an eight-wide, four-high eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// The stage shifts are 2, -1 and 0, and both cosine bit counts are 13.
    /// The column transform is a four-point kernel on vectors whose eight lanes hold the eight columns.
    /// The row transform is an eight-point kernel on vectors whose low four lanes hold the four rows.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform8x4(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform8x4<Dct4WideKernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform8x4<Adst4WideKernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform8x4<Dct4WideKernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform8x4<Adst4WideKernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform8x4<Adst4WideKernel, Dct8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform8x4<Dct4WideKernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform8x4<Adst4WideKernel, Adst8Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform8x4<Adst4WideKernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform8x4<Adst4WideKernel, Adst8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform8x4<Identity4WideKernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform8x4<Dct4WideKernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform8x4<Identity4WideKernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform8x4<Adst4WideKernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform8x4<Identity4WideKernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform8x4<Adst4WideKernel, Identity8Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform8x4<Identity4WideKernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one four-point column kernel and one eight-point row kernel to an eight-wide, four-high block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform8x4<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel4Wide
        where TRow : struct, IAv1Kernel8
    {
        // Each of the four rows loads into one full vector, and the first stage shift of 2 applies.
        ref short source = ref MemoryMarshal.GetReference(input);
        Vector128<short> r0;
        Vector128<short> r1;
        Vector128<short> r2;
        Vector128<short> r3;
        if (flipUpsideDown)
        {
            r3 = Vector128.LoadUnsafe(ref source) << 2;
            r2 = Vector128.LoadUnsafe(ref source, stride) << 2;
            r1 = Vector128.LoadUnsafe(ref source, 2 * stride) << 2;
            r0 = Vector128.LoadUnsafe(ref source, 3 * stride) << 2;
        }
        else
        {
            r0 = Vector128.LoadUnsafe(ref source) << 2;
            r1 = Vector128.LoadUnsafe(ref source, stride) << 2;
            r2 = Vector128.LoadUnsafe(ref source, 2 * stride) << 2;
            r3 = Vector128.LoadUnsafe(ref source, 3 * stride) << 2;
        }

        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, 13);

        // The second stage shift of -1 adds 1, half of the divisor 2, and then shifts right by 1. The addition saturates.
        Vector128<short> half = Vector128.Create((short)1);
        r0 = Vector128.AddSaturate(r0, half) >> 1;
        r1 = Vector128.AddSaturate(r1, half) >> 1;
        r2 = Vector128.AddSaturate(r2, half) >> 1;
        r3 = Vector128.AddSaturate(r3, half) >> 1;

        // An 8x8 transpose of the four rows gives eight outputs. Each output holds one column, with the four vertical frequencies in its low lanes.
        // Zero vectors fill the four unused input rows.
        Vector128<short> c0 = r0;
        Vector128<short> c1 = r1;
        Vector128<short> c2 = r2;
        Vector128<short> c3 = r3;
        Vector128<short> c4 = Vector128<short>.Zero;
        Vector128<short> c5 = Vector128<short>.Zero;
        Vector128<short> c6 = Vector128<short>.Zero;
        Vector128<short> c7 = Vector128<short>.Zero;
        Av1TransformKernels.Transpose8x8(ref c0, ref c1, ref c2, ref c3, ref c4, ref c5, ref c6, ref c7);

        // The row kernel transforms across the eight column vectors, lane by lane. A horizontal flip enters the columns in reverse order.
        // The outputs then hold one horizontal frequency each, with the four vertical frequencies in their low lanes.
        if (flipLeftToRight)
        {
            TRow.Transform(ref c7, ref c6, ref c5, ref c4, ref c3, ref c2, ref c1, ref c0, 13);
            (c0, c1, c2, c3, c4, c5, c6, c7) = (c7, c6, c5, c4, c3, c2, c1, c0);
        }
        else
        {
            TRow.Transform(ref c0, ref c1, ref c2, ref c3, ref c4, ref c5, ref c6, ref c7, 13);
        }

        // One more transpose gives four row-major vectors, one per vertical frequency, with the eight horizontal frequencies in their lanes.
        // The third shift is zero. The 2:1 shape scales each coefficient by the square root of two when the store widens it.
        Av1TransformKernels.Transpose8x8(ref c0, ref c1, ref c2, ref c3, ref c4, ref c5, ref c6, ref c7);
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        StoreRect8(c0, ref destination, 0);
        StoreRect8(c1, ref destination, 8);
        StoreRect8(c2, ref destination, 16);
        StoreRect8(c3, ref destination, 24);
    }

    /// <summary>
    /// Scales the low four lanes by the square root of two with rounding, widens them to thirty-two bits and stores them.
    /// </summary>
    /// <param name="values">The sixteen-bit coefficients in the low four lanes.</param>
    /// <param name="destination">The first coefficient of the destination.</param>
    /// <param name="offset">The index of the first stored coefficient.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRect4(Vector128<short> values, ref int destination, int offset)
        => ScaleRoundSqrt2(Vector128_.UnpackLow(values, Vector128.Create((short)1))).StoreUnsafe(ref destination, (nuint)offset);

    /// <summary>
    /// Scales eight lanes by the square root of two with rounding, widens them to thirty-two bits and stores them.
    /// </summary>
    /// <param name="values">The sixteen-bit coefficients.</param>
    /// <param name="destination">The first coefficient of the destination.</param>
    /// <param name="offset">The index of the first stored coefficient.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRect8(Vector128<short> values, ref int destination, int offset)
    {
        Vector128<short> one = Vector128.Create((short)1);
        ScaleRoundSqrt2(Vector128_.UnpackLow(values, one)).StoreUnsafe(ref destination, (nuint)offset);
        ScaleRoundSqrt2(Vector128_.UnpackHigh(values, one)).StoreUnsafe(ref destination, (nuint)(offset + 4));
    }

    /// <summary>
    /// Multiplies each value by the square root of two in fixed point, <see cref="Av1Transform1dMath.NewSqrt2"/>, and rounds.
    /// Each 32-bit lane holds one value in its low half and the constant one in its high half. Thus one multiply-add also adds the rounding term.
    /// </summary>
    /// <param name="valueAndOne">The interleaved values and ones.</param>
    /// <returns>The scaled values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> ScaleRoundSqrt2(Vector128<short> valueAndOne)
        => Vector128_.MultiplyAddAdjacent(
            valueAndOne,
            Av1TransformKernels.Pair((short)Av1Transform1dMath.NewSqrt2, (short)(1 << (Av1Transform1dMath.NewSqrt2Bits - 1))))
            >> Av1Transform1dMath.NewSqrt2Bits;

    /// <summary>
    /// The four-point forward DCT, on eight lanes.
    /// </summary>
    private readonly struct Dct4WideKernel : IAv1Kernel4Wide
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector128<int> rounding = Vector128.Create(1 << (cosBit - 1));
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            Vector128<short> p32p32 = Av1TransformKernels.Pair(c32, c32);
            Vector128<short> p32m32 = Av1TransformKernels.Pair(c32, (short)-c32);
            Vector128<short> p48p16 = Av1TransformKernels.Pair(c48, c16);
            Vector128<short> m16p48 = Av1TransformKernels.Pair((short)-c16, c48);

            // stage 1
            Vector128<short> x10 = Vector128.AddSaturate(in0, in3);
            Vector128<short> x13 = Vector128.SubtractSaturate(in0, in3);
            Vector128<short> x11 = Vector128.AddSaturate(in1, in2);
            Vector128<short> x12 = Vector128.SubtractSaturate(in1, in2);

            // stages 2 and 3: the first butterfly gives outputs 0 and 2, and the second butterfly gives outputs 1 and 3.
            Av1TransformKernels.Butterfly16(p32p32, p32m32, x10, x11, out in0, out in2, cosBit, rounding);
            Av1TransformKernels.Butterfly16(p48p16, m16p48, x12, x13, out in1, out in3, cosBit, rounding);
        }
    }

    /// <summary>
    /// The four-point forward ADST, on eight lanes.
    /// </summary>
    private readonly struct Adst4WideKernel : IAv1Kernel4Wide
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            int cosBit)
        {
            ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
            Vector128<int> rounding = Vector128.Create(1 << (cosBit - 1));
            short s1 = (short)sinpi[1];
            short s2 = (short)sinpi[2];
            short s3 = (short)sinpi[3];
            short s4 = (short)sinpi[4];
            Vector128<short> s1s2 = Av1TransformKernels.Pair(s1, s2);
            Vector128<short> s4m1 = Av1TransformKernels.Pair(s4, (short)-s1);
            Vector128<short> s3s4 = Av1TransformKernels.Pair(s3, s4);
            Vector128<short> m3s2 = Av1TransformKernels.Pair((short)-s3, s2);
            Vector128<short> s3s3 = Vector128.Create(s3);
            Vector128<short> zero = Vector128<short>.Zero;

            // The sum wraps at sixteen bits. It does not saturate.
            Vector128<short> in7 = in0 + in1;

            // Each output is formed twice, once for the low four lanes and once for the high four lanes.
            // The reason is that one multiply-add produces only four 32-bit sums.
            Vector128<short> u0Low = Vector128_.UnpackLow(in0, in1);
            Vector128<short> u0High = Vector128_.UnpackHigh(in0, in1);
            Vector128<short> u1Low = Vector128_.UnpackLow(in2, in3);
            Vector128<short> u1High = Vector128_.UnpackHigh(in2, in3);
            Vector128<short> u2Low = Vector128_.UnpackLow(in7, zero);
            Vector128<short> u2High = Vector128_.UnpackHigh(in7, zero);
            Vector128<short> u3Low = Vector128_.UnpackLow(in2, zero);
            Vector128<short> u3High = Vector128_.UnpackHigh(in2, zero);
            Vector128<short> u4Low = Vector128_.UnpackLow(in3, zero);
            Vector128<short> u4High = Vector128_.UnpackHigh(in3, zero);

            Vector128<int> v0Low = Vector128_.MultiplyAddAdjacent(u0Low, s1s2);
            Vector128<int> v0High = Vector128_.MultiplyAddAdjacent(u0High, s1s2);
            Vector128<int> v1Low = Vector128_.MultiplyAddAdjacent(u1Low, s3s4);
            Vector128<int> v1High = Vector128_.MultiplyAddAdjacent(u1High, s3s4);
            Vector128<int> v2Low = Vector128_.MultiplyAddAdjacent(u2Low, s3s3);
            Vector128<int> v2High = Vector128_.MultiplyAddAdjacent(u2High, s3s3);
            Vector128<int> v3Low = Vector128_.MultiplyAddAdjacent(u0Low, s4m1);
            Vector128<int> v3High = Vector128_.MultiplyAddAdjacent(u0High, s4m1);
            Vector128<int> v4Low = Vector128_.MultiplyAddAdjacent(u1Low, m3s2);
            Vector128<int> v4High = Vector128_.MultiplyAddAdjacent(u1High, m3s2);
            Vector128<int> v5Low = Vector128_.MultiplyAddAdjacent(u3Low, s3s3);
            Vector128<int> v5High = Vector128_.MultiplyAddAdjacent(u3High, s3s3);
            Vector128<int> v6Low = Vector128_.MultiplyAddAdjacent(u4Low, s3s3);
            Vector128<int> v6High = Vector128_.MultiplyAddAdjacent(u4High, s3s3);

            Vector128<int> w0Low = v0Low + v1Low;
            Vector128<int> w0High = v0High + v1High;
            Vector128<int> w1Low = v2Low - v6Low;
            Vector128<int> w1High = v2High - v6High;
            Vector128<int> w2Low = v3Low + v4Low;
            Vector128<int> w2High = v3High + v4High;
            Vector128<int> w3Low = w2Low - w0Low;
            Vector128<int> w3High = w2High - w0High;
            Vector128<int> w5Low = (v5Low << 2) - v5Low;
            Vector128<int> w5High = (v5High << 2) - v5High;
            Vector128<int> w6Low = w3Low + w5Low;
            Vector128<int> w6High = w3High + w5High;

            in0 = Vector128_.PackSignedSaturate((w0Low + rounding) >> cosBit, (w0High + rounding) >> cosBit);
            in1 = Vector128_.PackSignedSaturate((w1Low + rounding) >> cosBit, (w1High + rounding) >> cosBit);
            in2 = Vector128_.PackSignedSaturate((w2Low + rounding) >> cosBit, (w2High + rounding) >> cosBit);
            in3 = Vector128_.PackSignedSaturate((w6Low + rounding) >> cosBit, (w6High + rounding) >> cosBit);
        }
    }

    /// <summary>
    /// The four-point forward identity transform, on eight lanes, which scales by the square root of two.
    /// </summary>
    private readonly struct Identity4WideKernel : IAv1Kernel4Wide
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            int cosBit)
        {
            in0 = ScaleRound(in0);
            in1 = ScaleRound(in1);
            in2 = ScaleRound(in2);
            in3 = ScaleRound(in3);
        }

        /// <summary>
        /// Scales eight values by the square root of two with rounding and packs them back with saturation.
        /// </summary>
        /// <param name="value">The values.</param>
        /// <returns>The scaled values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> ScaleRound(Vector128<short> value)
        {
            Vector128<short> one = Vector128.Create((short)1);
            return Vector128_.PackSignedSaturate(
                ScaleRoundSqrt2(Vector128_.UnpackLow(value, one)),
                ScaleRoundSqrt2(Vector128_.UnpackHigh(value, one)));
        }
    }
}
