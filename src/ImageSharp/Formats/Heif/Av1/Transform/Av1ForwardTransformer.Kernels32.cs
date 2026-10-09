// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Vector forward transform kernels for the eight-bit block shapes with a thirty-two-sample side and no side over
/// thirty-two: thirty-two by thirty-two, sixteen by thirty-two and thirty-two by sixteen.
/// </content>
/// <remarks>
/// A thirty-two-sample axis allows only the DCT and the identity transform, so none of these shapes has a flip. Each
/// shape follows its <c>lowbd_fwd_txfm2d_*_avx2</c> function: the stages work on buffers of sixteen-lane vectors, and
/// a last transpose turns the reference output, one horizontal frequency per vector, into the row-major coefficient
/// order of this port.
/// </remarks>
internal static partial class Av1ForwardTransformer
{
    /// <summary>
    /// Applies a thirty-two-by-thirty-two eight-bit transform.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_32x32_avx2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_32X32]</c> (2, -4, 0)
    /// and both cosine bit counts are 12.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type: the DCT or the identity transform.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform32x32(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        if (transformType == Av1TransformType.DctDct)
        {
            Transform32x32<Dct32Kernel, Dct32Kernel>(input, stride, coefficients);
            return;
        }

        Transform32x32<Identity32Kernel, Identity32Kernel>(input, stride, coefficients);
    }

    /// <summary>
    /// Applies one thirty-two-point column kernel and one thirty-two-point row kernel to a thirty-two-by-thirty-two block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform32x32<TColumn, TRow>(ReadOnlySpan<short> input, uint stride, Span<int> coefficients)
        where TColumn : struct, IAv1Kernel32
        where TRow : struct, IAv1Kernel32
    {
        // The column buffer holds the thirty-two rows of one sixteen-column half. After the column pass, each vector of
        // the low buffer holds one column with vertical frequencies 0 to 15 in its lanes, and each vector of the high
        // buffer holds the same column with vertical frequencies 16 to 31. These are the two halves of buf1.
        InlineArray32<Vector256<short>> columnStorage = default;
        InlineArray32<Vector256<short>> lowStorage = default;
        InlineArray32<Vector256<short>> highStorage = default;
        ref Vector256<short> columns = ref columnStorage[0];
        ref Vector256<short> low = ref lowStorage[0];
        ref Vector256<short> high = ref highStorage[0];
        ref short source = ref MemoryMarshal.GetReference(input);

        for (nuint half = 0; half < 2; half++)
        {
            // Load each row of this sixteen-column half and scale it by the first shift (2).
            for (nuint row = 0; row < 32; row++)
            {
                Unsafe.Add(ref columns, row) = Vector256.LoadUnsafe(ref source, (row * stride) + (16 * half)) << 2;
            }

            TColumn.Transform(ref columns, 12);
            RoundShift4(ref columns, 32);

            // The two transpose_16bit_16x16_avx2 calls place the half's sixteen columns at their column positions.
            Av1TransformKernels.Transpose16x16(ref columns, ref Unsafe.Add(ref low, 16 * half));
            Av1TransformKernels.Transpose16x16(ref Unsafe.Add(ref columns, (nuint)16), ref Unsafe.Add(ref high, 16 * half));
        }

        // The row kernel transforms across the thirty-two column vectors. The third shift is zero.
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        TRow.Transform(ref low, 12);
        StoreRows32(ref low, ref destination, 0, false);
        TRow.Transform(ref high, 12);
        StoreRows32(ref high, ref destination, 16 * 32, false);
    }

    /// <summary>
    /// Applies a sixteen-wide, thirty-two-high eight-bit transform.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_16x32_avx2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_16X32]</c> (2, -4, 0),
    /// the column cosine bit count is 12 and the row cosine bit count is 13.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type: the DCT or the identity transform.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform16x32(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        if (transformType == Av1TransformType.DctDct)
        {
            Transform16x32<Dct32Kernel, Dct16Kernel>(input, stride, coefficients);
            return;
        }

        Transform16x32<Identity32Kernel, Identity16Kernel>(input, stride, coefficients);
    }

    /// <summary>
    /// Applies one thirty-two-point column kernel and one sixteen-point row kernel to a sixteen-wide, thirty-two-high
    /// block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform16x32<TColumn, TRow>(ReadOnlySpan<short> input, uint stride, Span<int> coefficients)
        where TColumn : struct, IAv1Kernel32
        where TRow : struct, IAv1Kernel16
    {
        // After the column pass, vectors 0 to 15 of the frequency buffer hold the sixteen columns with vertical
        // frequencies 0 to 15 in their lanes, and vectors 16 to 31 hold them with vertical frequencies 16 to 31.
        InlineArray32<Vector256<short>> columnStorage = default;
        InlineArray32<Vector256<short>> frequencyStorage = default;
        ref Vector256<short> columns = ref columnStorage[0];
        ref Vector256<short> frequencies = ref frequencyStorage[0];
        ref short source = ref MemoryMarshal.GetReference(input);

        for (nuint row = 0; row < 32; row++)
        {
            Unsafe.Add(ref columns, row) = Vector256.LoadUnsafe(ref source, row * stride) << 2;
        }

        TColumn.Transform(ref columns, 12);
        RoundShift4(ref columns, 32);
        Av1TransformKernels.Transpose16x16(ref columns, ref frequencies);
        Av1TransformKernels.Transpose16x16(ref Unsafe.Add(ref columns, (nuint)16), ref Unsafe.Add(ref frequencies, (nuint)16));

        // The row kernel runs once per group of sixteen vertical frequencies. The third shift is zero, and the 2:1
        // shape scales each coefficient by the square root of two while it widens.
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        Transform16<TRow>(ref frequencies, 13);
        Av1TransformKernels.Transpose16x16(ref frequencies, ref frequencies);
        Transform16<TRow>(ref Unsafe.Add(ref frequencies, (nuint)16), 13);
        Av1TransformKernels.Transpose16x16(ref Unsafe.Add(ref frequencies, (nuint)16), ref Unsafe.Add(ref frequencies, (nuint)16));
        for (nuint row = 0; row < 32; row++)
        {
            StoreRect16(Unsafe.Add(ref frequencies, row), ref destination, (int)row * 16);
        }
    }

    /// <summary>
    /// Applies a thirty-two-wide, sixteen-high eight-bit transform.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_fwd_txfm2d_32x16_avx2</c>. The shifts are <c>av1_fwd_txfm_shift_ls[TX_32X16]</c> (2, -4, 0)
    /// and both cosine bit counts are 13.
    /// </remarks>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="transformType">The compound transform type: the DCT or the identity transform.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform32x16(ReadOnlySpan<short> input, uint stride, Av1TransformType transformType, Span<int> coefficients)
    {
        if (transformType == Av1TransformType.DctDct)
        {
            Transform32x16<Dct16Kernel, Dct32Kernel>(input, stride, coefficients);
            return;
        }

        Transform32x16<Identity16Kernel, Identity32Kernel>(input, stride, coefficients);
    }

    /// <summary>
    /// Applies one sixteen-point column kernel and one thirty-two-point row kernel to a thirty-two-wide, sixteen-high
    /// block.
    /// </summary>
    /// <typeparam name="TColumn">The vertical transform kernel.</typeparam>
    /// <typeparam name="TRow">The horizontal transform kernel.</typeparam>
    /// <param name="input">The spatial residual samples.</param>
    /// <param name="stride">The number of input samples between rows.</param>
    /// <param name="coefficients">The destination transform coefficients.</param>
    private static void Transform32x16<TColumn, TRow>(ReadOnlySpan<short> input, uint stride, Span<int> coefficients)
        where TColumn : struct, IAv1Kernel16
        where TRow : struct, IAv1Kernel32
    {
        // The column buffer holds the sixteen rows of one sixteen-column half. After the column pass, each vector of
        // the frequency buffer holds one of the thirty-two columns with the sixteen vertical frequencies in its lanes.
        InlineArray16<Vector256<short>> columnStorage = default;
        InlineArray32<Vector256<short>> frequencyStorage = default;
        ref Vector256<short> columns = ref columnStorage[0];
        ref Vector256<short> frequencies = ref frequencyStorage[0];
        ref short source = ref MemoryMarshal.GetReference(input);

        for (nuint half = 0; half < 2; half++)
        {
            for (nuint row = 0; row < 16; row++)
            {
                Unsafe.Add(ref columns, row) = Vector256.LoadUnsafe(ref source, (row * stride) + (16 * half)) << 2;
            }

            Transform16<TColumn>(ref columns, 13);
            RoundShift4(ref columns, 16);
            Av1TransformKernels.Transpose16x16(ref columns, ref Unsafe.Add(ref frequencies, 16 * half));
        }

        // The third shift is zero, and the 2:1 shape scales each coefficient by the square root of two while it widens.
        ref int destination = ref MemoryMarshal.GetReference(coefficients);
        TRow.Transform(ref frequencies, 13);
        StoreRows32(ref frequencies, ref destination, 0, true);
    }

    /// <summary>
    /// Applies a sixteen-point kernel to sixteen consecutive vectors.
    /// </summary>
    /// <typeparam name="TKernel">The sixteen-point kernel.</typeparam>
    /// <param name="data">The first of the sixteen inputs and outputs.</param>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform16<TKernel>(ref Vector256<short> data, int cosBit)
        where TKernel : struct, IAv1Kernel16
        => TKernel.Transform(
            ref data,
            ref Unsafe.Add(ref data, (nuint)1),
            ref Unsafe.Add(ref data, (nuint)2),
            ref Unsafe.Add(ref data, (nuint)3),
            ref Unsafe.Add(ref data, (nuint)4),
            ref Unsafe.Add(ref data, (nuint)5),
            ref Unsafe.Add(ref data, (nuint)6),
            ref Unsafe.Add(ref data, (nuint)7),
            ref Unsafe.Add(ref data, (nuint)8),
            ref Unsafe.Add(ref data, (nuint)9),
            ref Unsafe.Add(ref data, (nuint)10),
            ref Unsafe.Add(ref data, (nuint)11),
            ref Unsafe.Add(ref data, (nuint)12),
            ref Unsafe.Add(ref data, (nuint)13),
            ref Unsafe.Add(ref data, (nuint)14),
            ref Unsafe.Add(ref data, (nuint)15),
            cosBit);

    /// <summary>
    /// Applies <c>round_shift_16bit_w16_avx2</c> with shift -4 to consecutive vectors: each lane adds one half with
    /// saturation and shifts right by four.
    /// </summary>
    /// <param name="data">The first vector.</param>
    /// <param name="count">The number of vectors.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RoundShift4(ref Vector256<short> data, nuint count)
    {
        Vector256<short> half = Vector256.Create((short)8);
        for (nuint i = 0; i < count; i++)
        {
            Unsafe.Add(ref data, i) = Vector256.AddSaturate(Unsafe.Add(ref data, i), half) >> 4;
        }
    }

    /// <summary>
    /// Stores sixteen rows of thirty-two coefficients from thirty-two vectors that each hold one horizontal frequency.
    /// </summary>
    /// <remarks>
    /// Vector <c>h</c> holds horizontal frequency <c>h</c> for sixteen vertical frequencies. A transpose of vectors 0 to
    /// 15 gives the left halves of the sixteen rows and a transpose of vectors 16 to 31 gives the right halves.
    /// </remarks>
    /// <param name="frequencies">The first of the thirty-two vectors. The vectors are transposed in place.</param>
    /// <param name="destination">The first coefficient of the block.</param>
    /// <param name="offset">The index of the first stored coefficient.</param>
    /// <param name="scale">Whether each coefficient is scaled by the square root of two, as the 2:1 shapes need.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRows32(ref Vector256<short> frequencies, ref int destination, int offset, bool scale)
    {
        ref Vector256<short> right = ref Unsafe.Add(ref frequencies, (nuint)16);
        Av1TransformKernels.Transpose16x16(ref frequencies, ref frequencies);
        Av1TransformKernels.Transpose16x16(ref right, ref right);

        for (nuint row = 0; row < 16; row++)
        {
            int rowOffset = offset + ((int)row * 32);
            if (scale)
            {
                StoreRect16(Unsafe.Add(ref frequencies, row), ref destination, rowOffset);
                StoreRect16(Unsafe.Add(ref right, row), ref destination, rowOffset + 16);
            }
            else
            {
                Av1TransformKernels.Store16(Unsafe.Add(ref frequencies, row), ref destination, rowOffset);
                Av1TransformKernels.Store16(Unsafe.Add(ref right, row), ref destination, rowOffset + 16);
            }
        }
    }

    /// <summary>
    /// Scales sixteen lanes by the square root of two with rounding, widens them to thirty-two bits and stores them, as
    /// <c>store_rect_16bit_to_32bit_avx2</c> does.
    /// </summary>
    /// <param name="values">The sixteen-bit coefficients.</param>
    /// <param name="destination">The first coefficient of the destination.</param>
    /// <param name="offset">The index of the first stored coefficient.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRect16(Vector256<short> values, ref int destination, int offset)
    {
        StoreRect8(values.GetLower(), ref destination, offset);
        StoreRect8(values.GetUpper(), ref destination, offset + 8);
    }

    /// <summary>
    /// The thirty-two-point DCT of <c>fdct16x32_avx2</c>.
    /// </summary>
    private readonly struct Dct32Kernel : IAv1Kernel32
    {
        /// <inheritdoc/>
        public static void Transform(ref Vector256<short> data, int cosBit)
        {
            ReadOnlySpan<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
            Vector256<int> rounding = Vector256.Create(1 << (cosBit - 1));

            // The weight pairs of the even part, as in the sixteen-point DCT.
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c24 = (short)cospi[24];
            short c40 = (short)cospi[40];
            Vector256<short> m32p32 = Av1TransformKernels.PairWide((short)-c32, c32);
            Vector256<short> p32p32 = Av1TransformKernels.PairWide(c32, c32);
            Vector256<short> m16p48 = Av1TransformKernels.PairWide((short)-c16, c48);
            Vector256<short> p48p16 = Av1TransformKernels.PairWide(c48, c16);
            Vector256<short> m48m16 = Av1TransformKernels.PairWide((short)-c48, (short)-c16);
            Vector256<short> m08p56 = Av1TransformKernels.PairWide((short)-c8, c56);
            Vector256<short> p56p08 = Av1TransformKernels.PairWide(c56, c8);
            Vector256<short> m40p24 = Av1TransformKernels.PairWide((short)-c40, c24);
            Vector256<short> p24p40 = Av1TransformKernels.PairWide(c24, c40);

            // stage 1
            Vector256<short> x0 = data;
            Vector256<short> x1 = Unsafe.Add(ref data, (nuint)1);
            Vector256<short> x2 = Unsafe.Add(ref data, (nuint)2);
            Vector256<short> x3 = Unsafe.Add(ref data, (nuint)3);
            Vector256<short> x4 = Unsafe.Add(ref data, (nuint)4);
            Vector256<short> x5 = Unsafe.Add(ref data, (nuint)5);
            Vector256<short> x6 = Unsafe.Add(ref data, (nuint)6);
            Vector256<short> x7 = Unsafe.Add(ref data, (nuint)7);
            Vector256<short> x8 = Unsafe.Add(ref data, (nuint)8);
            Vector256<short> x9 = Unsafe.Add(ref data, (nuint)9);
            Vector256<short> x10 = Unsafe.Add(ref data, (nuint)10);
            Vector256<short> x11 = Unsafe.Add(ref data, (nuint)11);
            Vector256<short> x12 = Unsafe.Add(ref data, (nuint)12);
            Vector256<short> x13 = Unsafe.Add(ref data, (nuint)13);
            Vector256<short> x14 = Unsafe.Add(ref data, (nuint)14);
            Vector256<short> x15 = Unsafe.Add(ref data, (nuint)15);
            Vector256<short> x16 = Unsafe.Add(ref data, (nuint)16);
            Vector256<short> x17 = Unsafe.Add(ref data, (nuint)17);
            Vector256<short> x18 = Unsafe.Add(ref data, (nuint)18);
            Vector256<short> x19 = Unsafe.Add(ref data, (nuint)19);
            Vector256<short> x20 = Unsafe.Add(ref data, (nuint)20);
            Vector256<short> x21 = Unsafe.Add(ref data, (nuint)21);
            Vector256<short> x22 = Unsafe.Add(ref data, (nuint)22);
            Vector256<short> x23 = Unsafe.Add(ref data, (nuint)23);
            Vector256<short> x24 = Unsafe.Add(ref data, (nuint)24);
            Vector256<short> x25 = Unsafe.Add(ref data, (nuint)25);
            Vector256<short> x26 = Unsafe.Add(ref data, (nuint)26);
            Vector256<short> x27 = Unsafe.Add(ref data, (nuint)27);
            Vector256<short> x28 = Unsafe.Add(ref data, (nuint)28);
            Vector256<short> x29 = Unsafe.Add(ref data, (nuint)29);
            Vector256<short> x30 = Unsafe.Add(ref data, (nuint)30);
            Vector256<short> x31 = Unsafe.Add(ref data, (nuint)31);
            Av1TransformKernels.AddSubtractWide(ref x0, ref x31);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x30);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x29);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x28);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x27);
            Av1TransformKernels.AddSubtractWide(ref x5, ref x26);
            Av1TransformKernels.AddSubtractWide(ref x6, ref x25);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x24);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x23);
            Av1TransformKernels.AddSubtractWide(ref x9, ref x22);
            Av1TransformKernels.AddSubtractWide(ref x10, ref x21);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x20);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x19);
            Av1TransformKernels.AddSubtractWide(ref x13, ref x18);
            Av1TransformKernels.AddSubtractWide(ref x14, ref x17);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x16);

            // stage 2
            Av1TransformKernels.AddSubtractWide(ref x0, ref x15);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x14);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x12);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x11);
            Av1TransformKernels.AddSubtractWide(ref x5, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x6, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x8);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x20, ref x27, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x21, ref x26, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x22, ref x25, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x23, ref x24, rounding, cosBit);

            // stage 3
            Av1TransformKernels.AddSubtractWide(ref x0, ref x7);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x6);
            Av1TransformKernels.AddSubtractWide(ref x2, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x3, ref x4);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x11, ref x12, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x16, ref x23);
            Av1TransformKernels.AddSubtractWide(ref x17, ref x22);
            Av1TransformKernels.AddSubtractWide(ref x18, ref x21);
            Av1TransformKernels.AddSubtractWide(ref x19, ref x20);
            Av1TransformKernels.AddSubtractWide(ref x31, ref x24);
            Av1TransformKernels.AddSubtractWide(ref x30, ref x25);
            Av1TransformKernels.AddSubtractWide(ref x29, ref x26);
            Av1TransformKernels.AddSubtractWide(ref x28, ref x27);

            // stage 4
            Av1TransformKernels.AddSubtractWide(ref x0, ref x3);
            Av1TransformKernels.AddSubtractWide(ref x1, ref x2);
            Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x5, ref x6, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x11);
            Av1TransformKernels.AddSubtractWide(ref x9, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x12);
            Av1TransformKernels.AddSubtractWide(ref x14, ref x13);
            Av1TransformKernels.ButterflyWide(m16p48, p48p16, ref x18, ref x29, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m16p48, p48p16, ref x19, ref x28, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m48m16, m16p48, ref x20, ref x27, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m48m16, m16p48, ref x21, ref x26, rounding, cosBit);

            // stage 5
            Av1TransformKernels.ButterflyWide(p32p32, Av1TransformKernels.PairWide(c32, (short)-c32), ref x0, ref x1, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p48p16, m16p48, ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x6);
            Av1TransformKernels.ButterflyWide(m16p48, p48p16, ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m48m16, m16p48, ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x16, ref x19);
            Av1TransformKernels.AddSubtractWide(ref x17, ref x18);
            Av1TransformKernels.AddSubtractWide(ref x23, ref x20);
            Av1TransformKernels.AddSubtractWide(ref x22, ref x21);
            Av1TransformKernels.AddSubtractWide(ref x24, ref x27);
            Av1TransformKernels.AddSubtractWide(ref x25, ref x26);
            Av1TransformKernels.AddSubtractWide(ref x31, ref x28);
            Av1TransformKernels.AddSubtractWide(ref x30, ref x29);

            // stage 6
            Av1TransformKernels.ButterflyWide(p56p08, m08p56, ref x4, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p24p40, m40p24, ref x5, ref x6, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x14);
            Av1TransformKernels.ButterflyWide(m08p56, p56p08, ref x17, ref x30, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c56, (short)-c8), m08p56, ref x18, ref x29, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(m40p24, p24p40, ref x21, ref x26, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c24, (short)-c40), m40p24, ref x22, ref x25, rounding, cosBit);

            // stage 7
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c36 = (short)cospi[36];
            short c28 = (short)cospi[28];
            short c20 = (short)cospi[20];
            short c44 = (short)cospi[44];
            short c52 = (short)cospi[52];
            short c12 = (short)cospi[12];
            Vector256<short> p60p04 = Av1TransformKernels.PairWide(c60, c4);
            Vector256<short> m04p60 = Av1TransformKernels.PairWide((short)-c4, c60);
            Vector256<short> p28p36 = Av1TransformKernels.PairWide(c28, c36);
            Vector256<short> m36p28 = Av1TransformKernels.PairWide((short)-c36, c28);
            Vector256<short> p44p20 = Av1TransformKernels.PairWide(c44, c20);
            Vector256<short> m20p44 = Av1TransformKernels.PairWide((short)-c20, c44);
            Vector256<short> p12p52 = Av1TransformKernels.PairWide(c12, c52);
            Vector256<short> m52p12 = Av1TransformKernels.PairWide((short)-c52, c12);
            Av1TransformKernels.ButterflyWide(p60p04, m04p60, ref x8, ref x15, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p28p36, m36p28, ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p44p20, m20p44, ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p12p52, m52p12, ref x11, ref x12, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x16, ref x17);
            Av1TransformKernels.AddSubtractWide(ref x19, ref x18);
            Av1TransformKernels.AddSubtractWide(ref x20, ref x21);
            Av1TransformKernels.AddSubtractWide(ref x23, ref x22);
            Av1TransformKernels.AddSubtractWide(ref x24, ref x25);
            Av1TransformKernels.AddSubtractWide(ref x27, ref x26);
            Av1TransformKernels.AddSubtractWide(ref x28, ref x29);
            Av1TransformKernels.AddSubtractWide(ref x31, ref x30);

            // stage 8
            short c2 = (short)cospi[2];
            short c62 = (short)cospi[62];
            short c34 = (short)cospi[34];
            short c30 = (short)cospi[30];
            short c18 = (short)cospi[18];
            short c46 = (short)cospi[46];
            short c50 = (short)cospi[50];
            short c14 = (short)cospi[14];
            short c10 = (short)cospi[10];
            short c54 = (short)cospi[54];
            short c42 = (short)cospi[42];
            short c22 = (short)cospi[22];
            short c26 = (short)cospi[26];
            short c38 = (short)cospi[38];
            short c58 = (short)cospi[58];
            short c6 = (short)cospi[6];
            Vector256<short> p62p02 = Av1TransformKernels.PairWide(c62, c2);
            Vector256<short> m02p62 = Av1TransformKernels.PairWide((short)-c2, c62);
            Vector256<short> p30p34 = Av1TransformKernels.PairWide(c30, c34);
            Vector256<short> m34p30 = Av1TransformKernels.PairWide((short)-c34, c30);
            Vector256<short> p46p18 = Av1TransformKernels.PairWide(c46, c18);
            Vector256<short> m18p46 = Av1TransformKernels.PairWide((short)-c18, c46);
            Vector256<short> p14p50 = Av1TransformKernels.PairWide(c14, c50);
            Vector256<short> m50p14 = Av1TransformKernels.PairWide((short)-c50, c14);
            Vector256<short> p54p10 = Av1TransformKernels.PairWide(c54, c10);
            Vector256<short> m10p54 = Av1TransformKernels.PairWide((short)-c10, c54);
            Vector256<short> p22p42 = Av1TransformKernels.PairWide(c22, c42);
            Vector256<short> m42p22 = Av1TransformKernels.PairWide((short)-c42, c22);
            Vector256<short> p38p26 = Av1TransformKernels.PairWide(c38, c26);
            Vector256<short> m26p38 = Av1TransformKernels.PairWide((short)-c26, c38);
            Vector256<short> p06p58 = Av1TransformKernels.PairWide(c6, c58);
            Vector256<short> m58p06 = Av1TransformKernels.PairWide((short)-c58, c6);
            Av1TransformKernels.ButterflyWide(p62p02, m02p62, ref x16, ref x31, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p30p34, m34p30, ref x17, ref x30, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p46p18, m18p46, ref x18, ref x29, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p14p50, m50p14, ref x19, ref x28, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p54p10, m10p54, ref x20, ref x27, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p22p42, m42p22, ref x21, ref x26, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p38p26, m26p38, ref x22, ref x25, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p06p58, m58p06, ref x23, ref x24, rounding, cosBit);

            // stage 9: output i is the stage value at the five-bit reversal of i.
            data = x0;
            Unsafe.Add(ref data, (nuint)1) = x16;
            Unsafe.Add(ref data, (nuint)2) = x8;
            Unsafe.Add(ref data, (nuint)3) = x24;
            Unsafe.Add(ref data, (nuint)4) = x4;
            Unsafe.Add(ref data, (nuint)5) = x20;
            Unsafe.Add(ref data, (nuint)6) = x12;
            Unsafe.Add(ref data, (nuint)7) = x28;
            Unsafe.Add(ref data, (nuint)8) = x2;
            Unsafe.Add(ref data, (nuint)9) = x18;
            Unsafe.Add(ref data, (nuint)10) = x10;
            Unsafe.Add(ref data, (nuint)11) = x26;
            Unsafe.Add(ref data, (nuint)12) = x6;
            Unsafe.Add(ref data, (nuint)13) = x22;
            Unsafe.Add(ref data, (nuint)14) = x14;
            Unsafe.Add(ref data, (nuint)15) = x30;
            Unsafe.Add(ref data, (nuint)16) = x1;
            Unsafe.Add(ref data, (nuint)17) = x17;
            Unsafe.Add(ref data, (nuint)18) = x9;
            Unsafe.Add(ref data, (nuint)19) = x25;
            Unsafe.Add(ref data, (nuint)20) = x5;
            Unsafe.Add(ref data, (nuint)21) = x21;
            Unsafe.Add(ref data, (nuint)22) = x13;
            Unsafe.Add(ref data, (nuint)23) = x29;
            Unsafe.Add(ref data, (nuint)24) = x3;
            Unsafe.Add(ref data, (nuint)25) = x19;
            Unsafe.Add(ref data, (nuint)26) = x11;
            Unsafe.Add(ref data, (nuint)27) = x27;
            Unsafe.Add(ref data, (nuint)28) = x7;
            Unsafe.Add(ref data, (nuint)29) = x23;
            Unsafe.Add(ref data, (nuint)30) = x15;
            Unsafe.Add(ref data, (nuint)31) = x31;
        }
    }

    /// <summary>
    /// The thirty-two-point identity transform of <c>fidentity16x32_avx2</c>, which scales by four.
    /// </summary>
    private readonly struct Identity32Kernel : IAv1Kernel32
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Transform(ref Vector256<short> data, int cosBit)
        {
            // The shift wraps like _mm256_slli_epi16.
            for (nuint i = 0; i < 32; i++)
            {
                Unsafe.Add(ref data, i) <<= 2;
            }
        }
    }
}
