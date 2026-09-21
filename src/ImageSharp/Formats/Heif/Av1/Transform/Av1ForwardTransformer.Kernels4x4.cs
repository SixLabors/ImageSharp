// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident four-by-four forward transform kernels.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Applies a four-by-four eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>av1_lowbd_fwd_txfm2d_4x4_sse2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_4X4]</c>
    /// (2, 0, 0) and both cosine bit counts are 13.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform4x4(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform4x4<Dct4Kernel, Dct4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform4x4<Adst4Kernel, Dct4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform4x4<Dct4Kernel, Adst4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform4x4<Adst4Kernel, Adst4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform4x4<Adst4Kernel, Dct4Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform4x4<Dct4Kernel, Adst4Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform4x4<Adst4Kernel, Adst4Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform4x4<Adst4Kernel, Adst4Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform4x4<Adst4Kernel, Adst4Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform4x4<Identity4Kernel, Identity4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform4x4<Dct4Kernel, Identity4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform4x4<Identity4Kernel, Dct4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform4x4<Adst4Kernel, Identity4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform4x4<Identity4Kernel, Adst4Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform4x4<Adst4Kernel, Identity4Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform4x4<Identity4Kernel, Adst4Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one column and one row kernel to a four-by-four block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform4x4<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel4
        where TRow : struct, IAv1Kernel4
    {
        ref short source = ref MemoryMarshal.GetReference(input);
        Vector128<short> r0;
        Vector128<short> r1;
        Vector128<short> r2;
        Vector128<short> r3;
        if (flipUpsideDown)
        {
            r3 = Load4(ref source, 0) << 2;
            r2 = Load4(ref source, stride) << 2;
            r1 = Load4(ref source, 2 * stride) << 2;
            r0 = Load4(ref source, 3 * stride) << 2;
        }
        else
        {
            r0 = Load4(ref source, 0) << 2;
            r1 = Load4(ref source, stride) << 2;
            r2 = Load4(ref source, 2 * stride) << 2;
            r3 = Load4(ref source, 3 * stride) << 2;
        }

        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, 13);
        Av1TransformKernels.Transpose4x4(ref r0, ref r1, ref r2, ref r3);

        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        if (flipLeftToRight)
        {
            TRow.Transform(ref r3, ref r2, ref r1, ref r0, 13);
            Av1TransformKernels.Transpose4x4(ref r3, ref r2, ref r1, ref r0);
            Store4(r3, ref destination, 0);
            Store4(r2, ref destination, 4);
            Store4(r1, ref destination, 8);
            Store4(r0, ref destination, 12);
            return;
        }

        TRow.Transform(ref r0, ref r1, ref r2, ref r3, 13);
        Av1TransformKernels.Transpose4x4(ref r0, ref r1, ref r2, ref r3);
        Store4(r0, ref destination, 0);
        Store4(r1, ref destination, 4);
        Store4(r2, ref destination, 8);
        Store4(r3, ref destination, 12);
    }

    /// <summary>
    /// Loads four sixteen-bit samples into the low lanes of a vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Load4(ref short source, uint offset)
        => Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref Unsafe.Add(ref source, (nint)offset)))).AsInt16();

    /// <summary>
    /// Widens the low four lanes to thirty-two bits and stores them, as <c>store_16bit_to_32bit_w4</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store4(Vector128<short> values, ref int destination, int offset)
        => Sse2.ShiftRightArithmetic(Sse2.UnpackLow(values, values).AsInt32(), 16).StoreUnsafe(ref destination, (nuint)offset);

    /// <summary>
    /// The four-point DCT of <c>fdct4x4_new_sse2</c>.
    /// </summary>
    private readonly struct Dct4Kernel : IAv1Kernel4
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

            Vector128<short> u0 = Sse2.UnpackLow(in0, in1);
            Vector128<short> u1 = Sse2.UnpackLow(in3, in2);
            Vector128<short> v0 = u0 + u1;
            Vector128<short> v1 = u0 - u1;

            Vector128<int> o0 = (Sse2.MultiplyAddAdjacent(v0, Av1TransformKernels.Pair(c32, c32)) + rounding) >> cosBit;
            Vector128<int> o2 = (Sse2.MultiplyAddAdjacent(v0, Av1TransformKernels.Pair(c32, (short)-c32)) + rounding) >> cosBit;
            Vector128<int> o1 = (Sse2.MultiplyAddAdjacent(v1, Av1TransformKernels.Pair(c16, c48)) + rounding) >> cosBit;
            Vector128<int> o3 = (Sse2.MultiplyAddAdjacent(v1, Av1TransformKernels.Pair(c48, (short)-c16)) + rounding) >> cosBit;

            in0 = Sse2.PackSignedSaturate(o0, o2);
            in1 = Sse2.PackSignedSaturate(o1, o3);
            in2 = Sse2.ShiftRightLogical128BitLane(in0, 8);
            in3 = Sse2.ShiftRightLogical128BitLane(in1, 8);
        }
    }

    /// <summary>
    /// The four-point ADST of <c>fadst4x4_new_sse2</c>.
    /// </summary>
    private readonly struct Adst4Kernel : IAv1Kernel4
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
            Vector128<short> zero = Vector128<short>.Zero;
            Vector128<short> in7 = in0 + in1;

            Vector128<short> u0 = Sse2.UnpackLow(in0, in1);
            Vector128<short> u1 = Sse2.UnpackLow(in2, in3);
            Vector128<short> u2 = Sse2.UnpackLow(in7, zero);
            Vector128<short> u3 = Sse2.UnpackLow(in2, zero);
            Vector128<short> u4 = Sse2.UnpackLow(in3, zero);
            Vector128<short> s3s3 = Vector128.Create(s3);

            Vector128<int> v0 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s1, s2));
            Vector128<int> v1 = Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair(s3, s4));
            Vector128<int> v2 = Sse2.MultiplyAddAdjacent(u2, s3s3);
            Vector128<int> v3 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s4, (short)-s1));
            Vector128<int> v4 = Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair((short)-s3, s2));
            Vector128<int> v5 = Sse2.MultiplyAddAdjacent(u3, s3s3);
            Vector128<int> v6 = Sse2.MultiplyAddAdjacent(u4, s3s3);

            Vector128<int> w0 = v0 + v1;
            Vector128<int> w1 = v2 - v6;
            Vector128<int> w2 = v3 + v4;
            Vector128<int> w3 = w2 - w0;
            Vector128<int> w4 = v5 << 2;
            Vector128<int> w5 = w4 - v5;
            Vector128<int> w6 = w3 + w5;

            w0 = (w0 + rounding) >> cosBit;
            w1 = (w1 + rounding) >> cosBit;
            w2 = (w2 + rounding) >> cosBit;
            w6 = (w6 + rounding) >> cosBit;

            in0 = Sse2.PackSignedSaturate(w0, w2);
            in1 = Sse2.PackSignedSaturate(w1, w6);
            in2 = Sse2.ShiftRightLogical128BitLane(in0, 8);
            in3 = Sse2.ShiftRightLogical128BitLane(in1, 8);
        }
    }

    /// <summary>
    /// The four-point identity transform of <c>fidentity4x4_new_sse2</c>, which scales by the square root of two.
    /// </summary>
    private readonly struct Identity4Kernel : IAv1Kernel4
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
            Vector128<short> one = Vector128.Create((short)1);
            Vector128<short> scale = Av1TransformKernels.Pair((short)Av1Transform1dMath.NewSqrt2, (short)(1 << (Av1Transform1dMath.NewSqrt2Bits - 1)));
            in0 = ScaleRound(in0, one, scale);
            in1 = ScaleRound(in1, one, scale);
            in2 = ScaleRound(in2, one, scale);
            in3 = ScaleRound(in3, one, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> ScaleRound(Vector128<short> value, Vector128<short> one, Vector128<short> scale)
        {
            Vector128<int> scaled = Sse2.MultiplyAddAdjacent(Sse2.UnpackLow(value, one), scale) >> Av1Transform1dMath.NewSqrt2Bits;
            return Sse2.PackSignedSaturate(scaled, scaled);
        }
    }
}
