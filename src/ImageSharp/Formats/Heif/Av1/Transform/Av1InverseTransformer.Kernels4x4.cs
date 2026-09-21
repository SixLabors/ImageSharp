// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident four-by-four inverse transform kernels for eight-bit blocks.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// Adds the inverse transform of a four-by-four eight-bit block to its prediction with every stage in registers.
    /// </summary>
    /// <remarks>
    /// This follows <c>lowbd_inv_txfm2d_add_4x4_ssse3</c>: the coefficient rows pack to sixteen bits, a transpose
    /// turns columns into lanes for the horizontal transform, the second transpose turns rows into lanes for the
    /// vertical transform, and the rows add to the prediction with clipping. The shifts are
    /// <c>av1_inv_txfm_shift_ls[TX_4X4]</c> (0, -4); the end of block selects no reduced kernel at this size.
    /// </remarks>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    public static void Inverse4x4(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        Av1TransformType transformType)
    {
        Av1Transform2dFlipConfiguration.GetAxes(transformType, out Av1TransformType1d columnType, out Av1TransformType1d rowType, out bool flipUpsideDown, out bool flipLeftToRight);
        switch (rowType)
        {
            case Av1TransformType1d.Dct:
                InverseColumn4x4<Dct4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight);
                break;
            case Av1TransformType1d.Adst:
            case Av1TransformType1d.FlipAdst:
                InverseColumn4x4<Adst4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight);
                break;
            default:
                InverseColumn4x4<Identity4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight);
                break;
        }
    }

    /// <summary>
    /// Selects the vertical kernel and runs the block.
    /// </summary>
    private static void InverseColumn4x4<TRow>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        Av1TransformType1d columnType,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel4
    {
        switch (columnType)
        {
            case Av1TransformType1d.Dct:
                Inverse4x4<TRow, Dct4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                break;
            case Av1TransformType1d.Adst:
            case Av1TransformType1d.FlipAdst:
                Inverse4x4<TRow, Adst4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                break;
            default:
                Inverse4x4<TRow, Identity4InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                break;
        }
    }

    /// <summary>
    /// Applies one horizontal and one vertical kernel to a four-by-four block and adds the result.
    /// </summary>
    /// <typeparam name="TRow">The horizontal kernel, applied first.</typeparam>
    /// <typeparam name="TColumn">The vertical kernel.</typeparam>
    private static void Inverse4x4<TRow, TColumn>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel4
        where TColumn : struct, IAv1Kernel4
    {
        ref int source = ref MemoryMarshal.GetReference(coefficients);
        Vector128<short> r0 = Load4Coefficients(ref source, 0);
        Vector128<short> r1 = Load4Coefficients(ref source, 4);
        Vector128<short> r2 = Load4Coefficients(ref source, 8);
        Vector128<short> r3 = Load4Coefficients(ref source, 12);

        // Lanes become vertical frequencies so the horizontal transform combines the four column vectors.
        Av1TransformKernels.Transpose4x4(ref r0, ref r1, ref r2, ref r3);
        TRow.Transform(ref r0, ref r1, ref r2, ref r3, InverseCosBit);

        // Each vector is now one spatial column; reversing their order mirrors the block horizontally, after
        // which row k sits in r(3 - k) and the vertical kernel runs over the reversed references.
        if (flipLeftToRight)
        {
            Av1TransformKernels.Transpose4x4(ref r3, ref r2, ref r1, ref r0);
            TColumn.Transform(ref r3, ref r2, ref r1, ref r0, InverseCosBit);
        }
        else
        {
            Av1TransformKernels.Transpose4x4(ref r0, ref r1, ref r2, ref r3);
            TColumn.Transform(ref r0, ref r1, ref r2, ref r3, InverseCosBit);
        }

        r0 = Av1TransformKernels.RoundShiftRight(r0, 4);
        r1 = Av1TransformKernels.RoundShiftRight(r1, 4);
        r2 = Av1TransformKernels.RoundShiftRight(r2, 4);
        r3 = Av1TransformKernels.RoundShiftRight(r3, 4);

        ref byte predictionBase = ref MemoryMarshal.GetReference(prediction);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        if (flipUpsideDown != flipLeftToRight)
        {
            AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r3);
            AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r2);
            AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r1);
            AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r0);
            return;
        }

        AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r0);
        AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r1);
        AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r2);
        AddRow4(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r3);
    }

    /// <summary>
    /// Loads four thirty-two-bit coefficients and packs them into the low lanes with saturation, as
    /// <c>load_32bit_to_16bit_w4</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Load4Coefficients(ref int source, int offset)
    {
        Vector128<int> values = Vector128.LoadUnsafe(ref source, (nuint)offset);
        return Sse2.PackSignedSaturate(values, values);
    }

    /// <summary>
    /// Adds one four-sample residual row to its prediction row with clipping, as <c>lowbd_write_buffer_4xn_sse2</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddRow4(ref byte prediction, int predictionStride, ref byte destination, int destinationStride, int row, Vector128<short> residual)
    {
        Vector128<byte> predicted = Vector128.CreateScalarUnsafe(Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref prediction, row * predictionStride))).AsByte();
        Vector128<short> sum = Sse2.AddSaturate(residual, Sse2.UnpackLow(predicted, Vector128<byte>.Zero).AsInt16());
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref destination, row * destinationStride), Sse2.PackUnsignedSaturate(sum, sum).AsUInt32().ToScalar());
    }

    /// <summary>
    /// Computes both outputs of four sixteen-bit butterflies, as <c>btf_16_4p_sse2</c> does.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Butterfly4(
        Vector128<short> weights0,
        Vector128<short> weights1,
        Vector128<short> input0,
        Vector128<short> input1,
        out Vector128<short> output0,
        out Vector128<short> output1,
        int cosBit,
        Vector128<int> rounding)
    {
        Vector128<short> pairs = Sse2.UnpackLow(input0, input1);
        Vector128<int> u = (Sse2.MultiplyAddAdjacent(pairs, weights0) + rounding) >> cosBit;
        Vector128<int> v = (Sse2.MultiplyAddAdjacent(pairs, weights1) + rounding) >> cosBit;
        output0 = Sse2.PackSignedSaturate(u, u);
        output1 = Sse2.PackSignedSaturate(v, v);
    }

    /// <summary>
    /// The four-point inverse DCT of <c>idct4_w4_sse2</c>.
    /// </summary>
    private readonly struct Dct4InverseKernel : IAv1Kernel4
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

            // stages 1 and 2
            Butterfly4(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), in0, in2, out Vector128<short> x0, out Vector128<short> x1, cosBit, rounding);
            Butterfly4(Av1TransformKernels.Pair(c48, (short)-c16), Av1TransformKernels.Pair(c16, c48), in1, in3, out Vector128<short> x2, out Vector128<short> x3, cosBit, rounding);

            // stage 3
            in0 = Sse2.AddSaturate(x0, x3);
            in3 = Sse2.SubtractSaturate(x0, x3);
            in1 = Sse2.AddSaturate(x1, x2);
            in2 = Sse2.SubtractSaturate(x1, x2);
        }
    }

    /// <summary>
    /// The four-point inverse ADST of <c>iadst4_w4_sse2</c>.
    /// </summary>
    private readonly struct Adst4InverseKernel : IAv1Kernel4
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

            Vector128<short> u0 = Sse2.UnpackLow(in0, in2);
            Vector128<short> u1 = Sse2.UnpackLow(in1, in3);

            // x0*sin1 + x2*sin4 + x1*sin3 + x3*sin2
            Vector128<int> x0 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s1, s4)) + Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair(s3, s2));

            // x0*sin2 - x2*sin1 + x1*sin3 - x3*sin4
            Vector128<int> x1 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s2, (short)-s1)) + Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair(s3, (short)-s4));

            // x0*sin3 - x2*sin3 + x3*sin3
            Vector128<int> x2 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s3, (short)-s3)) + Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair(0, s3));

            // x0*sin4 + x2*sin2 - x1*sin3 - x3*sin1
            Vector128<int> x3 = Sse2.MultiplyAddAdjacent(u0, Av1TransformKernels.Pair(s4, s2)) + Sse2.MultiplyAddAdjacent(u1, Av1TransformKernels.Pair((short)-s3, (short)-s1));

            x0 = (x0 + rounding) >> cosBit;
            x1 = (x1 + rounding) >> cosBit;
            x2 = (x2 + rounding) >> cosBit;
            x3 = (x3 + rounding) >> cosBit;
            in0 = Sse2.PackSignedSaturate(x0, x0);
            in1 = Sse2.PackSignedSaturate(x1, x1);
            in2 = Sse2.PackSignedSaturate(x2, x2);
            in3 = Sse2.PackSignedSaturate(x3, x3);
        }
    }

    /// <summary>
    /// The four-point inverse identity transform of <c>iidentity4_ssse3</c>, which scales by the square root of two.
    /// </summary>
    private readonly struct Identity4InverseKernel : IAv1Kernel4
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
            // The fractional part of the scale rounds through the high multiply and the integer part is the input itself.
            Vector128<short> scale = Vector128.Create((short)((Av1Transform1dMath.NewSqrt2 - (1 << Av1Transform1dMath.NewSqrt2Bits)) << (15 - Av1Transform1dMath.NewSqrt2Bits)));
            in0 = Sse2.AddSaturate(Ssse3.MultiplyHighRoundScale(in0, scale), in0);
            in1 = Sse2.AddSaturate(Ssse3.MultiplyHighRoundScale(in1, scale), in1);
            in2 = Sse2.AddSaturate(Ssse3.MultiplyHighRoundScale(in2, scale), in2);
            in3 = Sse2.AddSaturate(Ssse3.MultiplyHighRoundScale(in3, scale), in3);
        }
    }
}
