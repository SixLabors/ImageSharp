// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident forward transform kernels for the most frequent eight-bit block shapes.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Gets a value indicating whether the register-resident kernels are available on this machine.
    /// </summary>
    private static bool KernelsSupported => Av1TransformKernels.IsSupported;

    /// <summary>
    /// Applies an eight-by-eight eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>av1_lowbd_fwd_txfm2d_8x8_sse2</c>: the rows load into eight vectors whose lanes are
    /// columns, the column transform combines those vectors, one transpose turns columns into lanes for the
    /// row transform, and a second transpose restores the row-major coefficient order of this port. The shifts
    /// are <c>av1_fwd_txfm_shift_ls[TX_8X8]</c> (2, -1, 0) and both cosine bit counts are 13.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform8x8(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform8x8<Dct8Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform8x8<Adst8Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform8x8<Dct8Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform8x8<Adst8Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform8x8<Adst8Kernel, Dct8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform8x8<Dct8Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform8x8<Adst8Kernel, Adst8Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform8x8<Adst8Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform8x8<Adst8Kernel, Adst8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform8x8<Identity8Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform8x8<Dct8Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform8x8<Identity8Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform8x8<Adst8Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform8x8<Identity8Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform8x8<Adst8Kernel, Identity8Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform8x8<Identity8Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one column and one row kernel to an eight-by-eight block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform8x8<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel8
        where TRow : struct, IAv1Kernel8
    {
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
            r7 = Vector128.LoadUnsafe(ref source) << 2;
            r6 = Vector128.LoadUnsafe(ref source, stride) << 2;
            r5 = Vector128.LoadUnsafe(ref source, 2 * stride) << 2;
            r4 = Vector128.LoadUnsafe(ref source, 3 * stride) << 2;
            r3 = Vector128.LoadUnsafe(ref source, 4 * stride) << 2;
            r2 = Vector128.LoadUnsafe(ref source, 5 * stride) << 2;
            r1 = Vector128.LoadUnsafe(ref source, 6 * stride) << 2;
            r0 = Vector128.LoadUnsafe(ref source, 7 * stride) << 2;
        }
        else
        {
            r0 = Vector128.LoadUnsafe(ref source) << 2;
            r1 = Vector128.LoadUnsafe(ref source, stride) << 2;
            r2 = Vector128.LoadUnsafe(ref source, 2 * stride) << 2;
            r3 = Vector128.LoadUnsafe(ref source, 3 * stride) << 2;
            r4 = Vector128.LoadUnsafe(ref source, 4 * stride) << 2;
            r5 = Vector128.LoadUnsafe(ref source, 5 * stride) << 2;
            r6 = Vector128.LoadUnsafe(ref source, 6 * stride) << 2;
            r7 = Vector128.LoadUnsafe(ref source, 7 * stride) << 2;
        }

        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, 13);

        // round_shift_16bit with shift -1: add one half and shift, with saturation on the addition.
        Vector128<short> half = Vector128.Create((short)1);
        r0 = Sse2.AddSaturate(r0, half) >> 1;
        r1 = Sse2.AddSaturate(r1, half) >> 1;
        r2 = Sse2.AddSaturate(r2, half) >> 1;
        r3 = Sse2.AddSaturate(r3, half) >> 1;
        r4 = Sse2.AddSaturate(r4, half) >> 1;
        r5 = Sse2.AddSaturate(r5, half) >> 1;
        r6 = Sse2.AddSaturate(r6, half) >> 1;
        r7 = Sse2.AddSaturate(r7, half) >> 1;

        Av1TransformKernels.Transpose8x8(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        if (flipLeftToRight)
        {
            // After the transpose each vector is one column; reversing their order mirrors the block horizontally.
            TRow.Transform(ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0, 13);
            Av1TransformKernels.Transpose8x8(ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0);
            Store8x8(r7, r6, r5, r4, r3, r2, r1, r0, coefficients);
            return;
        }

        TRow.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, 13);
        Av1TransformKernels.Transpose8x8(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        Store8x8(r0, r1, r2, r3, r4, r5, r6, r7, coefficients);
    }

    /// <summary>
    /// Stores eight coefficient rows in row-major order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store8x8(
        Vector128<short> r0,
        Vector128<short> r1,
        Vector128<short> r2,
        Vector128<short> r3,
        Vector128<short> r4,
        Vector128<short> r5,
        Vector128<short> r6,
        Vector128<short> r7,
        Span<int> coefficients)
    {
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        Av1TransformKernels.Store8(r0, ref destination, 0);
        Av1TransformKernels.Store8(r1, ref destination, 8);
        Av1TransformKernels.Store8(r2, ref destination, 16);
        Av1TransformKernels.Store8(r3, ref destination, 24);
        Av1TransformKernels.Store8(r4, ref destination, 32);
        Av1TransformKernels.Store8(r5, ref destination, 40);
        Av1TransformKernels.Store8(r6, ref destination, 48);
        Av1TransformKernels.Store8(r7, ref destination, 56);
    }

    /// <summary>
    /// The eight-point DCT of <c>fdct8x8_new_sse2</c>.
    /// </summary>
    private readonly struct Dct8Kernel : IAv1Kernel8
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            ref Vector128<short> in4,
            ref Vector128<short> in5,
            ref Vector128<short> in6,
            ref Vector128<short> in7,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector128<int> rounding = Vector128.Create(1 << (cosBit - 1));
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c24 = (short)cospi[24];
            short c40 = (short)cospi[40];

            // stage 1
            Vector128<short> x10 = Sse2.AddSaturate(in0, in7);
            Vector128<short> x17 = Sse2.SubtractSaturate(in0, in7);
            Vector128<short> x11 = Sse2.AddSaturate(in1, in6);
            Vector128<short> x16 = Sse2.SubtractSaturate(in1, in6);
            Vector128<short> x12 = Sse2.AddSaturate(in2, in5);
            Vector128<short> x15 = Sse2.SubtractSaturate(in2, in5);
            Vector128<short> x13 = Sse2.AddSaturate(in3, in4);
            Vector128<short> x14 = Sse2.SubtractSaturate(in3, in4);

            // stage 2
            Vector128<short> x20 = Sse2.AddSaturate(x10, x13);
            Vector128<short> x23 = Sse2.SubtractSaturate(x10, x13);
            Vector128<short> x21 = Sse2.AddSaturate(x11, x12);
            Vector128<short> x22 = Sse2.SubtractSaturate(x11, x12);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair((short)-c32, c32), Av1TransformKernels.Pair(c32, c32), x15, x16, out Vector128<short> x25, out Vector128<short> x26, cosBit, rounding);

            // stage 3
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x20, x21, out Vector128<short> x30, out Vector128<short> x31, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c48, c16), Av1TransformKernels.Pair((short)-c16, c48), x22, x23, out Vector128<short> x32, out Vector128<short> x33, cosBit, rounding);
            Vector128<short> x34 = Sse2.AddSaturate(x14, x25);
            Vector128<short> x35 = Sse2.SubtractSaturate(x14, x25);
            Vector128<short> x36 = Sse2.SubtractSaturate(x17, x26);
            Vector128<short> x37 = Sse2.AddSaturate(x17, x26);

            // stages 4 and 5
            in0 = x30;
            in4 = x31;
            in2 = x32;
            in6 = x33;
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c56, c8), Av1TransformKernels.Pair((short)-c8, c56), x34, x37, out in1, out in7, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c24, c40), Av1TransformKernels.Pair((short)-c40, c24), x35, x36, out in5, out in3, cosBit, rounding);
        }
    }

    /// <summary>
    /// The eight-point ADST of <c>fadst8x8_new_sse2</c>.
    /// </summary>
    private readonly struct Adst8Kernel : IAv1Kernel8
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            ref Vector128<short> in4,
            ref Vector128<short> in5,
            ref Vector128<short> in6,
            ref Vector128<short> in7,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector128<int> rounding = Vector128.Create(1 << (cosBit - 1));
            Vector128<short> zero = Vector128<short>.Zero;
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c20 = (short)cospi[20];
            short c44 = (short)cospi[44];
            short c36 = (short)cospi[36];
            short c28 = (short)cospi[28];
            short c52 = (short)cospi[52];
            short c12 = (short)cospi[12];

            // stage 1
            Vector128<short> x10 = in0;
            Vector128<short> x11 = Sse2.SubtractSaturate(zero, in7);
            Vector128<short> x12 = Sse2.SubtractSaturate(zero, in3);
            Vector128<short> x13 = in4;
            Vector128<short> x14 = Sse2.SubtractSaturate(zero, in1);
            Vector128<short> x15 = in6;
            Vector128<short> x16 = in2;
            Vector128<short> x17 = Sse2.SubtractSaturate(zero, in5);

            // stage 2
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x12, x13, out Vector128<short> x22, out Vector128<short> x23, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x16, x17, out Vector128<short> x26, out Vector128<short> x27, cosBit, rounding);

            // stage 3
            Vector128<short> x30 = Sse2.AddSaturate(x10, x22);
            Vector128<short> x32 = Sse2.SubtractSaturate(x10, x22);
            Vector128<short> x31 = Sse2.AddSaturate(x11, x23);
            Vector128<short> x33 = Sse2.SubtractSaturate(x11, x23);
            Vector128<short> x34 = Sse2.AddSaturate(x14, x26);
            Vector128<short> x36 = Sse2.SubtractSaturate(x14, x26);
            Vector128<short> x35 = Sse2.AddSaturate(x15, x27);
            Vector128<short> x37 = Sse2.SubtractSaturate(x15, x27);

            // stage 4
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c16, c48), Av1TransformKernels.Pair(c48, (short)-c16), x34, x35, out Vector128<short> x44, out Vector128<short> x45, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair((short)-c48, c16), Av1TransformKernels.Pair(c16, c48), x36, x37, out Vector128<short> x46, out Vector128<short> x47, cosBit, rounding);

            // stages 5, 6 and 7
            Vector128<short> out7 = Sse2.AddSaturate(x30, x44);
            Vector128<short> out3 = Sse2.SubtractSaturate(x30, x44);
            Vector128<short> out0 = Sse2.AddSaturate(x31, x45);
            Vector128<short> out4 = Sse2.SubtractSaturate(x31, x45);
            Vector128<short> out5 = Sse2.AddSaturate(x32, x46);
            Vector128<short> out1 = Sse2.SubtractSaturate(x32, x46);
            Vector128<short> out2 = Sse2.AddSaturate(x33, x47);
            Vector128<short> out6 = Sse2.SubtractSaturate(x33, x47);

            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c4, c60), Av1TransformKernels.Pair(c60, (short)-c4), out7, out0, out in7, out in0, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c20, c44), Av1TransformKernels.Pair(c44, (short)-c20), out5, out2, out in5, out in2, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c36, c28), Av1TransformKernels.Pair(c28, (short)-c36), out3, out4, out in3, out in4, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c52, c12), Av1TransformKernels.Pair(c12, (short)-c52), out1, out6, out in1, out in6, cosBit, rounding);
        }
    }

    /// <summary>
    /// The eight-point identity transform of <c>fidentity8x8_new_sse2</c>, which doubles with saturation.
    /// </summary>
    private readonly struct Identity8Kernel : IAv1Kernel8
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector128<short> in0,
            ref Vector128<short> in1,
            ref Vector128<short> in2,
            ref Vector128<short> in3,
            ref Vector128<short> in4,
            ref Vector128<short> in5,
            ref Vector128<short> in6,
            ref Vector128<short> in7,
            int cosBit)
        {
            in0 = Sse2.AddSaturate(in0, in0);
            in1 = Sse2.AddSaturate(in1, in1);
            in2 = Sse2.AddSaturate(in2, in2);
            in3 = Sse2.AddSaturate(in3, in3);
            in4 = Sse2.AddSaturate(in4, in4);
            in5 = Sse2.AddSaturate(in5, in5);
            in6 = Sse2.AddSaturate(in6, in6);
            in7 = Sse2.AddSaturate(in7, in7);
        }
    }
}
