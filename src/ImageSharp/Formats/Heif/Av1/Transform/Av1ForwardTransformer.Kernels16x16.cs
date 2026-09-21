// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident sixteen-by-sixteen forward transform kernels for eight-bit blocks.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Gets a value indicating whether the sixteen-lane register-resident kernels are available on this machine.
    /// </summary>
    private static bool WideKernelsSupported => Av1TransformKernels.IsWideSupported;

    /// <summary>
    /// Applies a sixteen-by-sixteen eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_16x16_avx2</c>: the rows load into sixteen vectors whose lanes are
    /// columns, the column transform combines those vectors, one transpose turns columns into lanes for the
    /// row transform, and a second transpose restores the row-major coefficient order of this port. The shifts
    /// are <c>av1_fwd_txfm_shift_ls[TX_16X16]</c> (2, -2, 0); the column cosine bit count is 13 and the row
    /// cosine bit count is 12, from <c>av1_fwd_cos_bit_col</c> and <c>av1_fwd_cos_bit_row</c>.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform16x16(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform16x16<Dct16Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform16x16<Adst16Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform16x16<Dct16Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform16x16<Adst16Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform16x16<Adst16Kernel, Dct16Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform16x16<Dct16Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform16x16<Adst16Kernel, Adst16Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform16x16<Adst16Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform16x16<Adst16Kernel, Adst16Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform16x16<Identity16Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform16x16<Dct16Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform16x16<Identity16Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform16x16<Adst16Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform16x16<Identity16Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform16x16<Adst16Kernel, Identity16Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform16x16<Identity16Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one column and one row kernel to a sixteen-by-sixteen block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform16x16<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel16
        where TRow : struct, IAv1Kernel16
    {
        ref short source = ref MemoryMarshal.GetReference(input);
        Vector256<short> r0;
        Vector256<short> r1;
        Vector256<short> r2;
        Vector256<short> r3;
        Vector256<short> r4;
        Vector256<short> r5;
        Vector256<short> r6;
        Vector256<short> r7;
        Vector256<short> r8;
        Vector256<short> r9;
        Vector256<short> r10;
        Vector256<short> r11;
        Vector256<short> r12;
        Vector256<short> r13;
        Vector256<short> r14;
        Vector256<short> r15;
        if (flipUpsideDown)
        {
            r15 = Vector256.LoadUnsafe(ref source) << 2;
            r14 = Vector256.LoadUnsafe(ref source, stride) << 2;
            r13 = Vector256.LoadUnsafe(ref source, 2 * stride) << 2;
            r12 = Vector256.LoadUnsafe(ref source, 3 * stride) << 2;
            r11 = Vector256.LoadUnsafe(ref source, 4 * stride) << 2;
            r10 = Vector256.LoadUnsafe(ref source, 5 * stride) << 2;
            r9 = Vector256.LoadUnsafe(ref source, 6 * stride) << 2;
            r8 = Vector256.LoadUnsafe(ref source, 7 * stride) << 2;
            r7 = Vector256.LoadUnsafe(ref source, 8 * stride) << 2;
            r6 = Vector256.LoadUnsafe(ref source, 9 * stride) << 2;
            r5 = Vector256.LoadUnsafe(ref source, 10 * stride) << 2;
            r4 = Vector256.LoadUnsafe(ref source, 11 * stride) << 2;
            r3 = Vector256.LoadUnsafe(ref source, 12 * stride) << 2;
            r2 = Vector256.LoadUnsafe(ref source, 13 * stride) << 2;
            r1 = Vector256.LoadUnsafe(ref source, 14 * stride) << 2;
            r0 = Vector256.LoadUnsafe(ref source, 15 * stride) << 2;
        }
        else
        {
            r0 = Vector256.LoadUnsafe(ref source) << 2;
            r1 = Vector256.LoadUnsafe(ref source, stride) << 2;
            r2 = Vector256.LoadUnsafe(ref source, 2 * stride) << 2;
            r3 = Vector256.LoadUnsafe(ref source, 3 * stride) << 2;
            r4 = Vector256.LoadUnsafe(ref source, 4 * stride) << 2;
            r5 = Vector256.LoadUnsafe(ref source, 5 * stride) << 2;
            r6 = Vector256.LoadUnsafe(ref source, 6 * stride) << 2;
            r7 = Vector256.LoadUnsafe(ref source, 7 * stride) << 2;
            r8 = Vector256.LoadUnsafe(ref source, 8 * stride) << 2;
            r9 = Vector256.LoadUnsafe(ref source, 9 * stride) << 2;
            r10 = Vector256.LoadUnsafe(ref source, 10 * stride) << 2;
            r11 = Vector256.LoadUnsafe(ref source, 11 * stride) << 2;
            r12 = Vector256.LoadUnsafe(ref source, 12 * stride) << 2;
            r13 = Vector256.LoadUnsafe(ref source, 13 * stride) << 2;
            r14 = Vector256.LoadUnsafe(ref source, 14 * stride) << 2;
            r15 = Vector256.LoadUnsafe(ref source, 15 * stride) << 2;
        }

        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, 13);

        // round_shift_16bit_w16_avx2 with shift -2: add one half and shift, with saturation on the addition.
        Vector256<short> half = Vector256.Create((short)2);
        r0 = Avx2.AddSaturate(r0, half) >> 2;
        r1 = Avx2.AddSaturate(r1, half) >> 2;
        r2 = Avx2.AddSaturate(r2, half) >> 2;
        r3 = Avx2.AddSaturate(r3, half) >> 2;
        r4 = Avx2.AddSaturate(r4, half) >> 2;
        r5 = Avx2.AddSaturate(r5, half) >> 2;
        r6 = Avx2.AddSaturate(r6, half) >> 2;
        r7 = Avx2.AddSaturate(r7, half) >> 2;
        r8 = Avx2.AddSaturate(r8, half) >> 2;
        r9 = Avx2.AddSaturate(r9, half) >> 2;
        r10 = Avx2.AddSaturate(r10, half) >> 2;
        r11 = Avx2.AddSaturate(r11, half) >> 2;
        r12 = Avx2.AddSaturate(r12, half) >> 2;
        r13 = Avx2.AddSaturate(r13, half) >> 2;
        r14 = Avx2.AddSaturate(r14, half) >> 2;
        r15 = Avx2.AddSaturate(r15, half) >> 2;

        Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        if (flipLeftToRight)
        {
            // After the transpose each vector is one column; reversing their order mirrors the block horizontally.
            TRow.Transform(ref r15, ref r14, ref r13, ref r12, ref r11, ref r10, ref r9, ref r8, ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0, 12);
            Av1TransformKernels.Transpose16x16(ref r15, ref r14, ref r13, ref r12, ref r11, ref r10, ref r9, ref r8, ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0);
            Store16x16(r15, r14, r13, r12, r11, r10, r9, r8, r7, r6, r5, r4, r3, r2, r1, r0, coefficients);
            return;
        }

        TRow.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, 12);
        Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        Store16x16(r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15, coefficients);
    }

    /// <summary>
    /// Stores sixteen coefficient rows in row-major order.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store16x16(
        Vector256<short> r0,
        Vector256<short> r1,
        Vector256<short> r2,
        Vector256<short> r3,
        Vector256<short> r4,
        Vector256<short> r5,
        Vector256<short> r6,
        Vector256<short> r7,
        Vector256<short> r8,
        Vector256<short> r9,
        Vector256<short> r10,
        Vector256<short> r11,
        Vector256<short> r12,
        Vector256<short> r13,
        Vector256<short> r14,
        Vector256<short> r15,
        Span<int> coefficients)
    {
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        Av1TransformKernels.Store16(r0, ref destination, 0);
        Av1TransformKernels.Store16(r1, ref destination, 16);
        Av1TransformKernels.Store16(r2, ref destination, 32);
        Av1TransformKernels.Store16(r3, ref destination, 48);
        Av1TransformKernels.Store16(r4, ref destination, 64);
        Av1TransformKernels.Store16(r5, ref destination, 80);
        Av1TransformKernels.Store16(r6, ref destination, 96);
        Av1TransformKernels.Store16(r7, ref destination, 112);
        Av1TransformKernels.Store16(r8, ref destination, 128);
        Av1TransformKernels.Store16(r9, ref destination, 144);
        Av1TransformKernels.Store16(r10, ref destination, 160);
        Av1TransformKernels.Store16(r11, ref destination, 176);
        Av1TransformKernels.Store16(r12, ref destination, 192);
        Av1TransformKernels.Store16(r13, ref destination, 208);
        Av1TransformKernels.Store16(r14, ref destination, 224);
        Av1TransformKernels.Store16(r15, ref destination, 240);
    }

    /// <summary>
    /// The sixteen-point DCT of <c>fdct16x16_new_avx2</c>.
    /// </summary>
    private readonly struct Dct16Kernel : IAv1Kernel16
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector256<short> in0,
            ref Vector256<short> in1,
            ref Vector256<short> in2,
            ref Vector256<short> in3,
            ref Vector256<short> in4,
            ref Vector256<short> in5,
            ref Vector256<short> in6,
            ref Vector256<short> in7,
            ref Vector256<short> in8,
            ref Vector256<short> in9,
            ref Vector256<short> in10,
            ref Vector256<short> in11,
            ref Vector256<short> in12,
            ref Vector256<short> in13,
            ref Vector256<short> in14,
            ref Vector256<short> in15,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector256<int> rounding = Vector256.Create(1 << (cosBit - 1));
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c24 = (short)cospi[24];
            short c40 = (short)cospi[40];
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c36 = (short)cospi[36];
            short c28 = (short)cospi[28];
            short c20 = (short)cospi[20];
            short c44 = (short)cospi[44];
            short c52 = (short)cospi[52];
            short c12 = (short)cospi[12];
            Vector256<short> m32p32 = Av1TransformKernels.PairWide((short)-c32, c32);
            Vector256<short> p32p32 = Av1TransformKernels.PairWide(c32, c32);
            Vector256<short> p48p16 = Av1TransformKernels.PairWide(c48, c16);
            Vector256<short> m16p48 = Av1TransformKernels.PairWide((short)-c16, c48);

            // stage 1
            Vector256<short> x0 = Avx2.AddSaturate(in0, in15);
            Vector256<short> x15 = Avx2.SubtractSaturate(in0, in15);
            Vector256<short> x1 = Avx2.AddSaturate(in1, in14);
            Vector256<short> x14 = Avx2.SubtractSaturate(in1, in14);
            Vector256<short> x2 = Avx2.AddSaturate(in2, in13);
            Vector256<short> x13 = Avx2.SubtractSaturate(in2, in13);
            Vector256<short> x3 = Avx2.AddSaturate(in3, in12);
            Vector256<short> x12 = Avx2.SubtractSaturate(in3, in12);
            Vector256<short> x4 = Avx2.AddSaturate(in4, in11);
            Vector256<short> x11 = Avx2.SubtractSaturate(in4, in11);
            Vector256<short> x5 = Avx2.AddSaturate(in5, in10);
            Vector256<short> x10 = Avx2.SubtractSaturate(in5, in10);
            Vector256<short> x6 = Avx2.AddSaturate(in6, in9);
            Vector256<short> x9 = Avx2.SubtractSaturate(in6, in9);
            Vector256<short> x7 = Avx2.AddSaturate(in7, in8);
            Vector256<short> x8 = Avx2.SubtractSaturate(in7, in8);

            // stage 2
            Av1TransformKernels.AddSubtractWide(ref x0, ref x7);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x6);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x4);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x11, ref x12, rounding, cosBit);

            // stage 3
            Av1TransformKernels.AddSubtractWide(ref x0, ref x3);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x2);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x5, ref x6, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x11);
            Av1TransformKernels.AddSubtractWide(ref x9, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x12);
            Av1TransformKernels.AddSubtractWide(ref x14, ref x13);

            // stage 4
            Av1TransformKernels.ButterflyWide(p32p32, Av1TransformKernels.PairWide(c32, (short)-c32), ref x0, ref x1, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p48p16, m16p48, ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x6);
            Av1TransformKernels.ButterflyWide(m16p48, p48p16, ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c48, (short)-c16), m16p48, ref x10, ref x13, rounding, cosBit);

            // stage 5
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c56, c8), Av1TransformKernels.PairWide((short)-c8, c56), ref x4, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c24, c40), Av1TransformKernels.PairWide((short)-c40, c24), ref x5, ref x6, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x14);

            // stage 6
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c60, c4), Av1TransformKernels.PairWide((short)-c4, c60), ref x8, ref x15, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c28, c36), Av1TransformKernels.PairWide((short)-c36, c28), ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c44, c20), Av1TransformKernels.PairWide((short)-c20, c44), ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c12, c52), Av1TransformKernels.PairWide((short)-c52, c12), ref x11, ref x12, rounding, cosBit);

            // stage 7
            in0 = x0;
            in1 = x8;
            in2 = x4;
            in3 = x12;
            in4 = x2;
            in5 = x10;
            in6 = x6;
            in7 = x14;
            in8 = x1;
            in9 = x9;
            in10 = x5;
            in11 = x13;
            in12 = x3;
            in13 = x11;
            in14 = x7;
            in15 = x15;
        }
    }

    /// <summary>
    /// The sixteen-point ADST of <c>fadst16x16_new_avx2</c>.
    /// </summary>
    private readonly struct Adst16Kernel : IAv1Kernel16
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector256<short> in0,
            ref Vector256<short> in1,
            ref Vector256<short> in2,
            ref Vector256<short> in3,
            ref Vector256<short> in4,
            ref Vector256<short> in5,
            ref Vector256<short> in6,
            ref Vector256<short> in7,
            ref Vector256<short> in8,
            ref Vector256<short> in9,
            ref Vector256<short> in10,
            ref Vector256<short> in11,
            ref Vector256<short> in12,
            ref Vector256<short> in13,
            ref Vector256<short> in14,
            ref Vector256<short> in15,
            int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector256<int> rounding = Vector256.Create(1 << (cosBit - 1));
            Vector256<short> zero = Vector256<short>.Zero;
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c40 = (short)cospi[40];
            short c24 = (short)cospi[24];
            Vector256<short> p32p32 = Av1TransformKernels.PairWide(c32, c32);
            Vector256<short> p32m32 = Av1TransformKernels.PairWide(c32, (short)-c32);
            Vector256<short> p16p48 = Av1TransformKernels.PairWide(c16, c48);
            Vector256<short> p48m16 = Av1TransformKernels.PairWide(c48, (short)-c16);
            Vector256<short> m48p16 = Av1TransformKernels.PairWide((short)-c48, c16);
            Vector256<short> p08p56 = Av1TransformKernels.PairWide(c8, c56);
            Vector256<short> p40p24 = Av1TransformKernels.PairWide(c40, c24);

            // stage 1
            Vector256<short> x0 = in0;
            Vector256<short> x1 = Avx2.SubtractSaturate(zero, in15);
            Vector256<short> x2 = Avx2.SubtractSaturate(zero, in7);
            Vector256<short> x3 = in8;
            Vector256<short> x4 = Avx2.SubtractSaturate(zero, in3);
            Vector256<short> x5 = in12;
            Vector256<short> x6 = in4;
            Vector256<short> x7 = Avx2.SubtractSaturate(zero, in11);
            Vector256<short> x8 = Avx2.SubtractSaturate(zero, in1);
            Vector256<short> x9 = in14;
            Vector256<short> x10 = in6;
            Vector256<short> x11 = Avx2.SubtractSaturate(zero, in9);
            Vector256<short> x12 = in2;
            Vector256<short> x13 = Avx2.SubtractSaturate(zero, in13);
            Vector256<short> x14 = Avx2.SubtractSaturate(zero, in5);
            Vector256<short> x15 = in10;

            // stage 2
            Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x6, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x10, ref x11, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x14, ref x15, rounding, cosBit);

            // stage 3
            Av1TransformKernels.AddSubtractWide(ref x0, ref x2);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x3);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x6);
            Av1TransformKernels.AddSubtractWide(ref x5, ref x7);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x9, ref x11);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x14);
            Av1TransformKernels.AddSubtractWide(ref x13, ref x15);

            // stage 4
            Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x4, ref x5, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m48p16, p16p48, ref x6, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x12, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m48p16, p16p48, ref x14, ref x15, rounding, cosBit);

            // stage 5
            Av1TransformKernels.AddSubtractWide(ref x0, ref x4);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x6);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x7);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x12);
            Av1TransformKernels.AddSubtractWide(ref x9, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x10, ref x14);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x15);

            // stage 6
            Av1TransformKernels.ButterflyWide(p08p56, Av1TransformKernels.PairWide(c56, (short)-c8), ref x8, ref x9, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p40p24, Av1TransformKernels.PairWide(c24, (short)-c40), ref x10, ref x11, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c56, c8), p08p56, ref x12, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c24, c40), p40p24, ref x14, ref x15, rounding, cosBit);

            // stage 7
            Av1TransformKernels.AddSubtractWide(ref x0, ref x8);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x11);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x12);
            Av1TransformKernels.AddSubtractWide(ref x5, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x6, ref x14);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x15);

            // stage 8
            short c2 = (short)cospi[2];
            short c62 = (short)cospi[62];
            short c10 = (short)cospi[10];
            short c54 = (short)cospi[54];
            short c18 = (short)cospi[18];
            short c46 = (short)cospi[46];
            short c26 = (short)cospi[26];
            short c38 = (short)cospi[38];
            short c34 = (short)cospi[34];
            short c30 = (short)cospi[30];
            short c42 = (short)cospi[42];
            short c22 = (short)cospi[22];
            short c50 = (short)cospi[50];
            short c14 = (short)cospi[14];
            short c58 = (short)cospi[58];
            short c6 = (short)cospi[6];
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c2, c62), Av1TransformKernels.PairWide(c62, (short)-c2), ref x0, ref x1, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c10, c54), Av1TransformKernels.PairWide(c54, (short)-c10), ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c18, c46), Av1TransformKernels.PairWide(c46, (short)-c18), ref x4, ref x5, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c26, c38), Av1TransformKernels.PairWide(c38, (short)-c26), ref x6, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c34, c30), Av1TransformKernels.PairWide(c30, (short)-c34), ref x8, ref x9, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c42, c22), Av1TransformKernels.PairWide(c22, (short)-c42), ref x10, ref x11, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c50, c14), Av1TransformKernels.PairWide(c14, (short)-c50), ref x12, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c58, c6), Av1TransformKernels.PairWide(c6, (short)-c58), ref x14, ref x15, rounding, cosBit);

            // stage 9
            in0 = x1;
            in1 = x14;
            in2 = x3;
            in3 = x12;
            in4 = x5;
            in5 = x10;
            in6 = x7;
            in7 = x8;
            in8 = x9;
            in9 = x6;
            in10 = x11;
            in11 = x4;
            in12 = x13;
            in13 = x2;
            in14 = x15;
            in15 = x0;
        }
    }

    /// <summary>
    /// The sixteen-point identity transform of <c>fidentity16x16_new_avx2</c>, which scales by two times the
    /// square root of two with rounding.
    /// </summary>
    private readonly struct Identity16Kernel : IAv1Kernel16
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(
            ref Vector256<short> in0,
            ref Vector256<short> in1,
            ref Vector256<short> in2,
            ref Vector256<short> in3,
            ref Vector256<short> in4,
            ref Vector256<short> in5,
            ref Vector256<short> in6,
            ref Vector256<short> in7,
            ref Vector256<short> in8,
            ref Vector256<short> in9,
            ref Vector256<short> in10,
            ref Vector256<short> in11,
            ref Vector256<short> in12,
            ref Vector256<short> in13,
            ref Vector256<short> in14,
            ref Vector256<short> in15,
            int cosBit)
        {
            // scale_round_avx2 with 2 * NewSqrt2: each lane pairs with a one so that one multiply-add yields
            // value * scale + rounding before the shift by NewSqrt2Bits.
            Vector256<short> one = Vector256.Create((short)1);
            Vector256<short> scale = Av1TransformKernels.PairWide(2 * Av1Transform1dMath.NewSqrt2, 1 << (Av1Transform1dMath.NewSqrt2Bits - 1));
            in0 = Scale(in0, one, scale);
            in1 = Scale(in1, one, scale);
            in2 = Scale(in2, one, scale);
            in3 = Scale(in3, one, scale);
            in4 = Scale(in4, one, scale);
            in5 = Scale(in5, one, scale);
            in6 = Scale(in6, one, scale);
            in7 = Scale(in7, one, scale);
            in8 = Scale(in8, one, scale);
            in9 = Scale(in9, one, scale);
            in10 = Scale(in10, one, scale);
            in11 = Scale(in11, one, scale);
            in12 = Scale(in12, one, scale);
            in13 = Scale(in13, one, scale);
            in14 = Scale(in14, one, scale);
            in15 = Scale(in15, one, scale);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<short> Scale(Vector256<short> value, Vector256<short> one, Vector256<short> scale)
        {
            Vector256<int> lower = Avx2.MultiplyAddAdjacent(Avx2.UnpackLow(value, one), scale) >> Av1Transform1dMath.NewSqrt2Bits;
            Vector256<int> upper = Avx2.MultiplyAddAdjacent(Avx2.UnpackHigh(value, one), scale) >> Av1Transform1dMath.NewSqrt2Bits;
            return Avx2.PackSignedSaturate(lower, upper);
        }
    }
}
