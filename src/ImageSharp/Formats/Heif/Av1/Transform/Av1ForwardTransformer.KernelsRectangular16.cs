// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident forward transform kernels for the eight-by-sixteen and sixteen-by-eight eight-bit block shapes.
/// </content>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Applies an eight-wide, sixteen-high eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_8x16_avx2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_8X16]</c> (2, -2, 0)
    /// and both cosine bit counts are 13. The column transform is a sixteen-point kernel on the eight columns, as
    /// <c>col_txfm8x16_arr</c> selects. The row transform is an eight-point kernel on the sixteen rows, as
    /// <c>row_txfm8x16_arr</c> selects; it runs once for rows 0 to 7 and once for rows 8 to 15, which gives the same
    /// lanes as the sixteen-lane form because every kernel works lane by lane.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform8x16(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform8x16<Dct16Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform8x16<Adst16Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform8x16<Dct16Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform8x16<Adst16Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform8x16<Adst16Kernel, Dct8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform8x16<Dct16Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform8x16<Adst16Kernel, Adst8Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform8x16<Adst16Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform8x16<Adst16Kernel, Adst8Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform8x16<Identity16Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform8x16<Dct16Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform8x16<Identity16Kernel, Dct8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform8x16<Adst16Kernel, Identity8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform8x16<Identity16Kernel, Adst8Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform8x16<Adst16Kernel, Identity8Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform8x16<Identity16Kernel, Adst8Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one sixteen-point column kernel and one eight-point row kernel to an eight-wide, sixteen-high block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform8x16<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel16
        where TRow : struct, IAv1Kernel8
    {
        // The sixteen rows load into sixteen vectors whose eight lanes are the columns, scaled by the first shift (2).
        // The column kernel is the sixteen-lane form, so each row widens to a 256-bit vector. Its high lanes are never
        // read again, because every kernel works lane by lane.
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
            r15 = LoadRow8(ref source, 0);
            r14 = LoadRow8(ref source, stride);
            r13 = LoadRow8(ref source, 2 * stride);
            r12 = LoadRow8(ref source, 3 * stride);
            r11 = LoadRow8(ref source, 4 * stride);
            r10 = LoadRow8(ref source, 5 * stride);
            r9 = LoadRow8(ref source, 6 * stride);
            r8 = LoadRow8(ref source, 7 * stride);
            r7 = LoadRow8(ref source, 8 * stride);
            r6 = LoadRow8(ref source, 9 * stride);
            r5 = LoadRow8(ref source, 10 * stride);
            r4 = LoadRow8(ref source, 11 * stride);
            r3 = LoadRow8(ref source, 12 * stride);
            r2 = LoadRow8(ref source, 13 * stride);
            r1 = LoadRow8(ref source, 14 * stride);
            r0 = LoadRow8(ref source, 15 * stride);
        }
        else
        {
            r0 = LoadRow8(ref source, 0);
            r1 = LoadRow8(ref source, stride);
            r2 = LoadRow8(ref source, 2 * stride);
            r3 = LoadRow8(ref source, 3 * stride);
            r4 = LoadRow8(ref source, 4 * stride);
            r5 = LoadRow8(ref source, 5 * stride);
            r6 = LoadRow8(ref source, 6 * stride);
            r7 = LoadRow8(ref source, 7 * stride);
            r8 = LoadRow8(ref source, 8 * stride);
            r9 = LoadRow8(ref source, 9 * stride);
            r10 = LoadRow8(ref source, 10 * stride);
            r11 = LoadRow8(ref source, 11 * stride);
            r12 = LoadRow8(ref source, 12 * stride);
            r13 = LoadRow8(ref source, 13 * stride);
            r14 = LoadRow8(ref source, 14 * stride);
            r15 = LoadRow8(ref source, 15 * stride);
        }

        TColumn.Transform(
            ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, 13);

        // round_shift_16bit with shift -2 on the low eight lanes: add one half and shift, with saturation on the addition.
        Vector128<short> half = Vector128.Create((short)2);
        Vector128<short> l0 = Vector128.AddSaturate(r0.GetLower(), half) >> 2;
        Vector128<short> l1 = Vector128.AddSaturate(r1.GetLower(), half) >> 2;
        Vector128<short> l2 = Vector128.AddSaturate(r2.GetLower(), half) >> 2;
        Vector128<short> l3 = Vector128.AddSaturate(r3.GetLower(), half) >> 2;
        Vector128<short> l4 = Vector128.AddSaturate(r4.GetLower(), half) >> 2;
        Vector128<short> l5 = Vector128.AddSaturate(r5.GetLower(), half) >> 2;
        Vector128<short> l6 = Vector128.AddSaturate(r6.GetLower(), half) >> 2;
        Vector128<short> l7 = Vector128.AddSaturate(r7.GetLower(), half) >> 2;
        Vector128<short> u0 = Vector128.AddSaturate(r8.GetLower(), half) >> 2;
        Vector128<short> u1 = Vector128.AddSaturate(r9.GetLower(), half) >> 2;
        Vector128<short> u2 = Vector128.AddSaturate(r10.GetLower(), half) >> 2;
        Vector128<short> u3 = Vector128.AddSaturate(r11.GetLower(), half) >> 2;
        Vector128<short> u4 = Vector128.AddSaturate(r12.GetLower(), half) >> 2;
        Vector128<short> u5 = Vector128.AddSaturate(r13.GetLower(), half) >> 2;
        Vector128<short> u6 = Vector128.AddSaturate(r14.GetLower(), half) >> 2;
        Vector128<short> u7 = Vector128.AddSaturate(r15.GetLower(), half) >> 2;

        // The two transpose_16bit_8x8 calls: each vector of the first set now holds one column of rows 0 to 7, and the
        // same vector of the second set holds that column for rows 8 to 15.
        Av1TransformKernels.Transpose8x8(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7);
        Av1TransformKernels.Transpose8x8(ref u0, ref u1, ref u2, ref u3, ref u4, ref u5, ref u6, ref u7);

        // The row kernel transforms across the eight column vectors, lane by lane, for each half. A horizontal flip
        // enters the columns in reverse order, as flip_buf_sse2 does. The outputs then hold one horizontal frequency
        // each, with the vertical frequencies of the half in their lanes.
        if (flipLeftToRight)
        {
            TRow.Transform(ref l7, ref l6, ref l5, ref l4, ref l3, ref l2, ref l1, ref l0, 13);
            TRow.Transform(ref u7, ref u6, ref u5, ref u4, ref u3, ref u2, ref u1, ref u0, 13);
            (l0, l1, l2, l3, l4, l5, l6, l7) = (l7, l6, l5, l4, l3, l2, l1, l0);
            (u0, u1, u2, u3, u4, u5, u6, u7) = (u7, u6, u5, u4, u3, u2, u1, u0);
        }
        else
        {
            TRow.Transform(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7, 13);
            TRow.Transform(ref u0, ref u1, ref u2, ref u3, ref u4, ref u5, ref u6, ref u7, 13);
        }

        // The reference stores each horizontal frequency as one row of sixteen. This port keeps coefficients
        // row-major, so a transpose of each half gives the rows of vertical frequencies 0 to 7 and 8 to 15, with the
        // eight horizontal frequencies in their lanes. The third shift is zero, and the 2:1 shape scales each
        // coefficient by the square root of two while it widens, as store_rect_buffer_16bit_to_32bit_w16_avx2 does.
        Av1TransformKernels.Transpose8x8(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7);
        Av1TransformKernels.Transpose8x8(ref u0, ref u1, ref u2, ref u3, ref u4, ref u5, ref u6, ref u7);
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        StoreRect8(l0, ref destination, 0);
        StoreRect8(l1, ref destination, 8);
        StoreRect8(l2, ref destination, 16);
        StoreRect8(l3, ref destination, 24);
        StoreRect8(l4, ref destination, 32);
        StoreRect8(l5, ref destination, 40);
        StoreRect8(l6, ref destination, 48);
        StoreRect8(l7, ref destination, 56);
        StoreRect8(u0, ref destination, 64);
        StoreRect8(u1, ref destination, 72);
        StoreRect8(u2, ref destination, 80);
        StoreRect8(u3, ref destination, 88);
        StoreRect8(u4, ref destination, 96);
        StoreRect8(u5, ref destination, 104);
        StoreRect8(u6, ref destination, 112);
        StoreRect8(u7, ref destination, 120);
    }

    /// <summary>
    /// Applies a sixteen-wide, eight-high eight-bit transform with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_16x8_avx2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_16X8]</c> (2, -2, 0)
    /// and both cosine bit counts are 13. The column transform is an eight-point kernel on the sixteen columns, as
    /// <c>col_txfm16x8_arr</c> selects; it runs once for columns 0 to 7 and once for columns 8 to 15. The row
    /// transform is a sixteen-point kernel on the eight rows, as <c>row_txfm16x8_arr</c> selects.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform16x8(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        switch (transformType)
        {
            case Av1TransformType.DctDct:
                Transform16x8<Dct8Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstDct:
                Transform16x8<Adst8Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.DctAdst:
                Transform16x8<Dct8Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.AdstAdst:
                Transform16x8<Adst8Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.FlipAdstDct:
                Transform16x8<Adst8Kernel, Dct16Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.DctFlipAdst:
                Transform16x8<Dct8Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstFlipAdst:
                Transform16x8<Adst8Kernel, Adst16Kernel>(input, stride, coefficients, true, true);
                break;
            case Av1TransformType.AdstFlipAdst:
                Transform16x8<Adst8Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
            case Av1TransformType.FlipAdstAdst:
                Transform16x8<Adst8Kernel, Adst16Kernel>(input, stride, coefficients, true, false);
                break;
            case Av1TransformType.Identity:
                Transform16x8<Identity8Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalDct:
                Transform16x8<Dct8Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalDct:
                Transform16x8<Identity8Kernel, Dct16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalAdst:
                Transform16x8<Adst8Kernel, Identity16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.HorizontalAdst:
                Transform16x8<Identity8Kernel, Adst16Kernel>(input, stride, coefficients, false, false);
                break;
            case Av1TransformType.VerticalFlipAdst:
                Transform16x8<Adst8Kernel, Identity16Kernel>(input, stride, coefficients, true, false);
                break;
            default:
                Transform16x8<Identity8Kernel, Adst16Kernel>(input, stride, coefficients, false, true);
                break;
        }
    }

    /// <summary>
    /// Applies one eight-point column kernel and one sixteen-point row kernel to a sixteen-wide, eight-high block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    /// <param name="flipUpsideDown">Whether the rows enter in reverse order.</param>
    /// <param name="flipLeftToRight">Whether the columns enter the row transform in reverse order.</param>
    private static void Transform16x8<TColumn, TRow>(
        ReadOnlySpan<short> input,
        uint stride,
        Span<int> coefficients,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TColumn : struct, IAv1Kernel8
        where TRow : struct, IAv1Kernel16
    {
        // Each of the eight rows loads as a left vector (columns 0 to 7) and a right vector (columns 8 to 15), scaled
        // by the first shift (2), as the two load_buffer_16bit_to_16bit calls do.
        ref short source = ref MemoryMarshal.GetReference(input);
        Vector128<short> l0;
        Vector128<short> l1;
        Vector128<short> l2;
        Vector128<short> l3;
        Vector128<short> l4;
        Vector128<short> l5;
        Vector128<short> l6;
        Vector128<short> l7;
        Vector128<short> h0;
        Vector128<short> h1;
        Vector128<short> h2;
        Vector128<short> h3;
        Vector128<short> h4;
        Vector128<short> h5;
        Vector128<short> h6;
        Vector128<short> h7;
        if (flipUpsideDown)
        {
            (l7, h7) = LoadRow16(ref source, 0);
            (l6, h6) = LoadRow16(ref source, stride);
            (l5, h5) = LoadRow16(ref source, 2 * stride);
            (l4, h4) = LoadRow16(ref source, 3 * stride);
            (l3, h3) = LoadRow16(ref source, 4 * stride);
            (l2, h2) = LoadRow16(ref source, 5 * stride);
            (l1, h1) = LoadRow16(ref source, 6 * stride);
            (l0, h0) = LoadRow16(ref source, 7 * stride);
        }
        else
        {
            (l0, h0) = LoadRow16(ref source, 0);
            (l1, h1) = LoadRow16(ref source, stride);
            (l2, h2) = LoadRow16(ref source, 2 * stride);
            (l3, h3) = LoadRow16(ref source, 3 * stride);
            (l4, h4) = LoadRow16(ref source, 4 * stride);
            (l5, h5) = LoadRow16(ref source, 5 * stride);
            (l6, h6) = LoadRow16(ref source, 6 * stride);
            (l7, h7) = LoadRow16(ref source, 7 * stride);
        }

        // The eight-point column kernel on sixteen lanes is the eight-lane kernel on each half.
        TColumn.Transform(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7, 13);
        TColumn.Transform(ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7, 13);

        // round_shift_16bit_w16_avx2 with shift -2: add one half and shift, with saturation on the addition.
        Vector128<short> half = Vector128.Create((short)2);
        l0 = Vector128.AddSaturate(l0, half) >> 2;
        l1 = Vector128.AddSaturate(l1, half) >> 2;
        l2 = Vector128.AddSaturate(l2, half) >> 2;
        l3 = Vector128.AddSaturate(l3, half) >> 2;
        l4 = Vector128.AddSaturate(l4, half) >> 2;
        l5 = Vector128.AddSaturate(l5, half) >> 2;
        l6 = Vector128.AddSaturate(l6, half) >> 2;
        l7 = Vector128.AddSaturate(l7, half) >> 2;
        h0 = Vector128.AddSaturate(h0, half) >> 2;
        h1 = Vector128.AddSaturate(h1, half) >> 2;
        h2 = Vector128.AddSaturate(h2, half) >> 2;
        h3 = Vector128.AddSaturate(h3, half) >> 2;
        h4 = Vector128.AddSaturate(h4, half) >> 2;
        h5 = Vector128.AddSaturate(h5, half) >> 2;
        h6 = Vector128.AddSaturate(h6, half) >> 2;
        h7 = Vector128.AddSaturate(h7, half) >> 2;

        // transpose_16bit_16x8_avx2 with extract_reg: after the two transposes, the left set holds columns 0 to 7 and
        // the right set holds columns 8 to 15, each with the eight rows in its lanes.
        Av1TransformKernels.Transpose8x8(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7);
        Av1TransformKernels.Transpose8x8(ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7);

        // The sixteen-point row kernel is the sixteen-lane form, so each column widens to a 256-bit vector whose high
        // lanes are never read again. A horizontal flip enters the sixteen columns in reverse order.
        Vector256<short> c0;
        Vector256<short> c1;
        Vector256<short> c2;
        Vector256<short> c3;
        Vector256<short> c4;
        Vector256<short> c5;
        Vector256<short> c6;
        Vector256<short> c7;
        Vector256<short> c8;
        Vector256<short> c9;
        Vector256<short> c10;
        Vector256<short> c11;
        Vector256<short> c12;
        Vector256<short> c13;
        Vector256<short> c14;
        Vector256<short> c15;
        if (flipLeftToRight)
        {
            c0 = h7.ToVector256Unsafe();
            c1 = h6.ToVector256Unsafe();
            c2 = h5.ToVector256Unsafe();
            c3 = h4.ToVector256Unsafe();
            c4 = h3.ToVector256Unsafe();
            c5 = h2.ToVector256Unsafe();
            c6 = h1.ToVector256Unsafe();
            c7 = h0.ToVector256Unsafe();
            c8 = l7.ToVector256Unsafe();
            c9 = l6.ToVector256Unsafe();
            c10 = l5.ToVector256Unsafe();
            c11 = l4.ToVector256Unsafe();
            c12 = l3.ToVector256Unsafe();
            c13 = l2.ToVector256Unsafe();
            c14 = l1.ToVector256Unsafe();
            c15 = l0.ToVector256Unsafe();
        }
        else
        {
            c0 = l0.ToVector256Unsafe();
            c1 = l1.ToVector256Unsafe();
            c2 = l2.ToVector256Unsafe();
            c3 = l3.ToVector256Unsafe();
            c4 = l4.ToVector256Unsafe();
            c5 = l5.ToVector256Unsafe();
            c6 = l6.ToVector256Unsafe();
            c7 = l7.ToVector256Unsafe();
            c8 = h0.ToVector256Unsafe();
            c9 = h1.ToVector256Unsafe();
            c10 = h2.ToVector256Unsafe();
            c11 = h3.ToVector256Unsafe();
            c12 = h4.ToVector256Unsafe();
            c13 = h5.ToVector256Unsafe();
            c14 = h6.ToVector256Unsafe();
            c15 = h7.ToVector256Unsafe();
        }

        TRow.Transform(
            ref c0, ref c1, ref c2, ref c3, ref c4, ref c5, ref c6, ref c7, ref c8, ref c9, ref c10, ref c11, ref c12, ref c13, ref c14, ref c15, 13);

        // The outputs hold one horizontal frequency each, with the eight vertical frequencies in their low lanes. This
        // port keeps coefficients row-major, so a transpose of frequencies 0 to 7 and one of 8 to 15 give the left and
        // right halves of each row. The third shift is zero, and the 2:1 shape scales each coefficient by the square
        // root of two while it widens, as store_rect_buffer_16bit_to_32bit_w8 does.
        l0 = c0.GetLower();
        l1 = c1.GetLower();
        l2 = c2.GetLower();
        l3 = c3.GetLower();
        l4 = c4.GetLower();
        l5 = c5.GetLower();
        l6 = c6.GetLower();
        l7 = c7.GetLower();
        h0 = c8.GetLower();
        h1 = c9.GetLower();
        h2 = c10.GetLower();
        h3 = c11.GetLower();
        h4 = c12.GetLower();
        h5 = c13.GetLower();
        h6 = c14.GetLower();
        h7 = c15.GetLower();
        Av1TransformKernels.Transpose8x8(ref l0, ref l1, ref l2, ref l3, ref l4, ref l5, ref l6, ref l7);
        Av1TransformKernels.Transpose8x8(ref h0, ref h1, ref h2, ref h3, ref h4, ref h5, ref h6, ref h7);
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        StoreRect8(l0, ref destination, 0);
        StoreRect8(h0, ref destination, 8);
        StoreRect8(l1, ref destination, 16);
        StoreRect8(h1, ref destination, 24);
        StoreRect8(l2, ref destination, 32);
        StoreRect8(h2, ref destination, 40);
        StoreRect8(l3, ref destination, 48);
        StoreRect8(h3, ref destination, 56);
        StoreRect8(l4, ref destination, 64);
        StoreRect8(h4, ref destination, 72);
        StoreRect8(l5, ref destination, 80);
        StoreRect8(h5, ref destination, 88);
        StoreRect8(l6, ref destination, 96);
        StoreRect8(h6, ref destination, 104);
        StoreRect8(l7, ref destination, 112);
        StoreRect8(h7, ref destination, 120);
    }

    /// <summary>
    /// Loads one row of eight samples, scaled by the first shift (2), into the low lanes of a 256-bit vector.
    /// </summary>
    /// <param name="source">The first residual sample.</param>
    /// <param name="offset">The index of the first sample of the row.</param>
    /// <returns>The scaled row. The high lanes are undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> LoadRow8(ref short source, uint offset)
        => (Vector128.LoadUnsafe(ref source, offset) << 2).ToVector256Unsafe();

    /// <summary>
    /// Loads one row of sixteen samples, scaled by the first shift (2), as a left and a right vector.
    /// </summary>
    /// <param name="source">The first residual sample.</param>
    /// <param name="offset">The index of the first sample of the row.</param>
    /// <returns>Columns 0 to 7 and columns 8 to 15 of the row.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector128<short> Left, Vector128<short> Right) LoadRow16(ref short source, uint offset)
        => (Vector128.LoadUnsafe(ref source, offset) << 2, Vector128.LoadUnsafe(ref source, offset + 8) << 2);
}
