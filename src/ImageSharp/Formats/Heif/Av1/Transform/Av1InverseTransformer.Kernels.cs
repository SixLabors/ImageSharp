// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident inverse transform kernels for eight-bit eight-by-eight blocks.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// The cosine bit count of every inverse transform stage.
    /// </summary>
    private const int InverseCosBit = 12;

    /// <summary>
    /// Adds the inverse transform of an eight-by-eight eight-bit block to its prediction with every stage in registers.
    /// </summary>
    /// <remarks>
    /// The coefficient rows pack to sixteen bits with saturation, which gives the sixteen-bit row-input clamp. A transpose puts each coefficient row in one
    /// lane, so the horizontal transform runs on all eight rows at once. The second transpose puts each column in one lane for the vertical transform. Then the
    /// rows add to the prediction with clipping. The stage shifts are -1 and -4. An end of block of one selects the DC-only DCT and ADST kernels.
    /// </remarks>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="endOfBlock">The one-based final nonzero scan position.</param>
    public static void Inverse8x8(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        Av1TransformType transformType,
        int endOfBlock)
    {
        // For an 8x8 block, every end position after the first needs both axes complete. Only an end position of one uses the DC-only kernels.
        Av1Transform2dFlipConfiguration.GetAxes(transformType, out Av1TransformType1d columnType, out Av1TransformType1d rowType, out bool flipUpsideDown, out bool flipLeftToRight);
        bool dc = endOfBlock == 1;
        switch (rowType)
        {
            case Av1TransformType1d.Dct:
                if (dc)
                {
                    InverseColumn8x8<Dct8DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight, dc);
                }
                else
                {
                    InverseColumn8x8<Dct8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight, dc);
                }

                break;
            case Av1TransformType1d.Adst:
            case Av1TransformType1d.FlipAdst:
                if (dc)
                {
                    InverseColumn8x8<Adst8DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight, dc);
                }
                else
                {
                    InverseColumn8x8<Adst8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight, dc);
                }

                break;
            default:
                InverseColumn8x8<Identity8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnType, flipUpsideDown, flipLeftToRight, dc);
                break;
        }
    }

    /// <summary>
    /// Selects the vertical kernel and runs the block.
    /// </summary>
    /// <typeparam name="TRow">The horizontal kernel, applied first.</typeparam>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="columnType">The one-dimensional type of the vertical transform.</param>
    /// <param name="flipUpsideDown">Whether the residual block is mirrored vertically.</param>
    /// <param name="flipLeftToRight">Whether the residual block is mirrored horizontally.</param>
    /// <param name="dcColumn">Whether only the DC coefficient is coded, which selects the DC-only DCT or ADST vertical kernel.</param>
    private static void InverseColumn8x8<TRow>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        Av1TransformType1d columnType,
        bool flipUpsideDown,
        bool flipLeftToRight,
        bool dcColumn)
        where TRow : struct, IAv1Kernel8
    {
        switch (columnType)
        {
            case Av1TransformType1d.Dct:
                if (dcColumn)
                {
                    Inverse8x8<TRow, Dct8DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    Inverse8x8<TRow, Dct8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }

                break;
            case Av1TransformType1d.Adst:
            case Av1TransformType1d.FlipAdst:
                if (dcColumn)
                {
                    Inverse8x8<TRow, Adst8DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    Inverse8x8<TRow, Adst8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }

                break;
            default:
                Inverse8x8<TRow, Identity8InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                break;
        }
    }

    /// <summary>
    /// Applies one horizontal and one vertical kernel to an eight-by-eight block and adds the result.
    /// </summary>
    /// <typeparam name="TRow">The horizontal kernel, applied first.</typeparam>
    /// <typeparam name="TColumn">The vertical kernel.</typeparam>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="flipUpsideDown">Whether the residual block is mirrored vertically.</param>
    /// <param name="flipLeftToRight">Whether the residual block is mirrored horizontally.</param>
    private static void Inverse8x8<TRow, TColumn>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel8
        where TColumn : struct, IAv1Kernel8
    {
        ref int source = ref MemoryMarshal.GetReference(coefficients);
        Vector128<short> r0 = Av1TransformKernels.Load8(ref source, 0);
        Vector128<short> r1 = Av1TransformKernels.Load8(ref source, 8);
        Vector128<short> r2 = Av1TransformKernels.Load8(ref source, 16);
        Vector128<short> r3 = Av1TransformKernels.Load8(ref source, 24);
        Vector128<short> r4 = Av1TransformKernels.Load8(ref source, 32);
        Vector128<short> r5 = Av1TransformKernels.Load8(ref source, 40);
        Vector128<short> r6 = Av1TransformKernels.Load8(ref source, 48);
        Vector128<short> r7 = Av1TransformKernels.Load8(ref source, 56);

        // Lanes become vertical frequencies so the horizontal transform combines the eight column vectors.
        Av1TransformKernels.Transpose8x8(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
        TRow.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, InverseCosBit);
        r0 = Av1TransformKernels.RoundShiftRight(r0, 1);
        r1 = Av1TransformKernels.RoundShiftRight(r1, 1);
        r2 = Av1TransformKernels.RoundShiftRight(r2, 1);
        r3 = Av1TransformKernels.RoundShiftRight(r3, 1);
        r4 = Av1TransformKernels.RoundShiftRight(r4, 1);
        r5 = Av1TransformKernels.RoundShiftRight(r5, 1);
        r6 = Av1TransformKernels.RoundShiftRight(r6, 1);
        r7 = Av1TransformKernels.RoundShiftRight(r7, 1);

        // Each vector is now one spatial column. The reversed order mirrors the block horizontally.
        if (flipLeftToRight)
        {
            // The reversed transpose leaves row k in r(7 - k). The swaps restore the natural row order.
            Av1TransformKernels.Transpose8x8(ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0);
            TColumn.Transform(ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0, InverseCosBit);
            (r0, r7) = (r7, r0);
            (r1, r6) = (r6, r1);
            (r2, r5) = (r5, r2);
            (r3, r4) = (r4, r3);
        }
        else
        {
            Av1TransformKernels.Transpose8x8(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7);
            TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, InverseCosBit);
        }

        r0 = Av1TransformKernels.RoundShiftRight(r0, 4);
        r1 = Av1TransformKernels.RoundShiftRight(r1, 4);
        r2 = Av1TransformKernels.RoundShiftRight(r2, 4);
        r3 = Av1TransformKernels.RoundShiftRight(r3, 4);
        r4 = Av1TransformKernels.RoundShiftRight(r4, 4);
        r5 = Av1TransformKernels.RoundShiftRight(r5, 4);
        r6 = Av1TransformKernels.RoundShiftRight(r6, 4);
        r7 = Av1TransformKernels.RoundShiftRight(r7, 4);

        ref byte predictionBase = ref MemoryMarshal.GetReference(prediction);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        if (flipUpsideDown)
        {
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r7);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r6);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r5);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r4);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 4, r3);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 5, r2);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 6, r1);
            AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 7, r0);
            return;
        }

        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r0);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r1);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r2);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r3);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 4, r4);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 5, r5);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 6, r6);
        AddRow(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 7, r7);
    }

    /// <summary>
    /// Adds one residual row to its prediction row.
    /// </summary>
    /// <param name="prediction">The first predicted sample of the block.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The first reconstructed sample of the block.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="row">The row index.</param>
    /// <param name="residual">The eight residuals of the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddRow(ref byte prediction, int predictionStride, ref byte destination, int destinationStride, int row, Vector128<short> residual)
        => Av1TransformKernels.AddClip8(
            ref Unsafe.Add(ref prediction, row * predictionStride),
            residual,
            ref Unsafe.Add(ref destination, row * destinationStride));

    /// <summary>
    /// Replaces two values with their saturated sum and difference.
    /// </summary>
    /// <param name="left">The first value, replaced by the sum.</param>
    /// <param name="right">The second value, replaced by the difference.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddSubtract(ref Vector128<short> left, ref Vector128<short> right)
    {
        Vector128<short> sum = Vector128.AddSaturate(left, right);
        right = Vector128.SubtractSaturate(left, right);
        left = sum;
    }

    /// <summary>
    /// The eight-point inverse DCT on eight lanes.
    /// </summary>
    private readonly struct Dct8InverseKernel : IAv1Kernel8
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

            // Stage 1: bit-reversed input order. The even inputs feed the four-point DCT half, and the odd inputs feed the rotation half.
            Vector128<short> x0 = in0;
            Vector128<short> x1 = in4;
            Vector128<short> x2 = in2;
            Vector128<short> x3 = in6;
            Vector128<short> x4 = in1;
            Vector128<short> x5 = in5;
            Vector128<short> x6 = in3;
            Vector128<short> x7 = in7;

            // Stage 2: rotate the odd pairs (x4, x7) and (x5, x6) with the cosine-table weights 56 and 8, and 24 and 40.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c56, (short)-c8), Av1TransformKernels.Pair(c8, c56), x4, x7, out x4, out x7, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c24, (short)-c40), Av1TransformKernels.Pair(c40, c24), x5, x6, out x5, out x6, cosBit, rounding);

            // Stage 3: for the even half, the cos(pi/4) butterfly and the rotation with weights 48 and 16. For the odd half, saturated sums and differences.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x0, x1, out x0, out x1, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c48, (short)-c16), Av1TransformKernels.Pair(c16, c48), x2, x3, out x2, out x3, cosBit, rounding);
            Vector128<short> t4 = Vector128.AddSaturate(x4, x5);
            x5 = Vector128.SubtractSaturate(x4, x5);
            x4 = t4;
            Vector128<short> t6 = Vector128.SubtractSaturate(x7, x6);
            x7 = Vector128.AddSaturate(x7, x6);
            x6 = t6;

            // Stage 4: the even half completes its four-point DCT, and the cos(pi/4) butterfly combines x5 and x6.
            Vector128<short> t0 = Vector128.AddSaturate(x0, x3);
            x3 = Vector128.SubtractSaturate(x0, x3);
            x0 = t0;
            Vector128<short> t1 = Vector128.AddSaturate(x1, x2);
            x2 = Vector128.SubtractSaturate(x1, x2);
            x1 = t1;
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair((short)-c32, c32), Av1TransformKernels.Pair(c32, c32), x5, x6, out x5, out x6, cosBit, rounding);

            // Stage 5: the output butterflies combine the even and odd halves with saturation.
            in0 = Vector128.AddSaturate(x0, x7);
            in7 = Vector128.SubtractSaturate(x0, x7);
            in1 = Vector128.AddSaturate(x1, x6);
            in6 = Vector128.SubtractSaturate(x1, x6);
            in2 = Vector128.AddSaturate(x2, x5);
            in5 = Vector128.SubtractSaturate(x2, x5);
            in3 = Vector128.AddSaturate(x3, x4);
            in4 = Vector128.SubtractSaturate(x3, x4);
        }
    }

    /// <summary>
    /// The DC-only eight-point inverse DCT on eight lanes.
    /// </summary>
    private readonly struct Dct8DcInverseKernel : IAv1Kernel8
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
            short c32 = (short)Av1SinusConstants.CosinusPi(cosBit)[32];
            Av1TransformKernels.Scale16(c32, c32, in0, out Vector128<short> x0, out _);
            in0 = x0;
            in1 = x0;
            in2 = x0;
            in3 = x0;
            in4 = x0;
            in5 = x0;
            in6 = x0;
            in7 = x0;
        }
    }

    /// <summary>
    /// The eight-point inverse ADST on eight lanes.
    /// </summary>
    private readonly struct Adst8InverseKernel : IAv1Kernel8
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
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c20 = (short)cospi[20];
            short c44 = (short)cospi[44];
            short c36 = (short)cospi[36];
            short c28 = (short)cospi[28];
            short c52 = (short)cospi[52];
            short c12 = (short)cospi[12];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c32 = (short)cospi[32];

            // Stage 1: the ADST input permutation.
            Vector128<short> x0 = in7;
            Vector128<short> x1 = in0;
            Vector128<short> x2 = in5;
            Vector128<short> x3 = in2;
            Vector128<short> x4 = in3;
            Vector128<short> x5 = in4;
            Vector128<short> x6 = in1;
            Vector128<short> x7 = in6;

            // Stage 2: four rotations with the odd cosine-table weights.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c4, c60), Av1TransformKernels.Pair(c60, (short)-c4), x0, x1, out x0, out x1, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c20, c44), Av1TransformKernels.Pair(c44, (short)-c20), x2, x3, out x2, out x3, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c36, c28), Av1TransformKernels.Pair(c28, (short)-c36), x4, x5, out x4, out x5, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c52, c12), Av1TransformKernels.Pair(c12, (short)-c52), x6, x7, out x6, out x7, cosBit, rounding);

            // Stage 3: saturated sums and differences between the two halves.
            AddSubtract(ref x0, ref x4);
            AddSubtract(ref x1, ref x5);
            AddSubtract(ref x2, ref x6);
            AddSubtract(ref x3, ref x7);

            // Stage 4: rotate the upper half with the weights 16 and 48.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c16, c48), Av1TransformKernels.Pair(c48, (short)-c16), x4, x5, out x4, out x5, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair((short)-c48, c16), Av1TransformKernels.Pair(c16, c48), x6, x7, out x6, out x7, cosBit, rounding);

            // Stage 5: saturated sums and differences within each half.
            AddSubtract(ref x0, ref x2);
            AddSubtract(ref x1, ref x3);
            AddSubtract(ref x4, ref x6);
            AddSubtract(ref x5, ref x7);

            // Stage 6: the cos(pi/4) butterflies.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x2, x3, out x2, out x3, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x6, x7, out x6, out x7, cosBit, rounding);

            // Stage 7: the ADST output permutation, which negates every odd output with saturation.
            in0 = x0;
            in1 = Vector128.SubtractSaturate(zero, x4);
            in2 = x6;
            in3 = Vector128.SubtractSaturate(zero, x2);
            in4 = x3;
            in5 = Vector128.SubtractSaturate(zero, x7);
            in6 = x5;
            in7 = Vector128.SubtractSaturate(zero, x1);
        }
    }

    /// <summary>
    /// The DC-only eight-point inverse ADST on eight lanes.
    /// </summary>
    private readonly struct Adst8DcInverseKernel : IAv1Kernel8
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
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            short c32 = (short)cospi[32];

            // Only in0 is nonzero, so stages 1 and 2 reduce to the input rotation with the cosine-table weights 60 and -4. Each later add-subtract stage copies
            // its nonzero input to both outputs, so each pair of stages reduces to one multiply step.
            Av1TransformKernels.Scale16(c60, (short)-c4, in0, out Vector128<short> x0, out Vector128<short> x1);

            // Stages 3 and 4: x4 and x5 start as copies of x0 and x1 and take the rotation with weights 16 and 48.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c16, c48), Av1TransformKernels.Pair(c48, (short)-c16), x0, x1, out Vector128<short> x4, out Vector128<short> x5, cosBit, rounding);

            // Stages 5 and 6: (x2, x3) start as copies of (x0, x1), and (x6, x7) start as copies of (x4, x5). Each pair takes the cos(pi/4) butterfly.
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x0, x1, out Vector128<short> x2, out Vector128<short> x3, cosBit, rounding);
            Av1TransformKernels.Butterfly16(Av1TransformKernels.Pair(c32, c32), Av1TransformKernels.Pair(c32, (short)-c32), x4, x5, out Vector128<short> x6, out Vector128<short> x7, cosBit, rounding);

            // Stage 7: the ADST output permutation, which negates every odd output with saturation.
            in0 = x0;
            in1 = Vector128.SubtractSaturate(zero, x4);
            in2 = x6;
            in3 = Vector128.SubtractSaturate(zero, x2);
            in4 = x3;
            in5 = Vector128.SubtractSaturate(zero, x7);
            in6 = x5;
            in7 = Vector128.SubtractSaturate(zero, x1);
        }
    }

    /// <summary>
    /// The eight-point inverse identity transform on eight lanes, which doubles with saturation.
    /// </summary>
    private readonly struct Identity8InverseKernel : IAv1Kernel8
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
            in0 = Vector128.AddSaturate(in0, in0);
            in1 = Vector128.AddSaturate(in1, in1);
            in2 = Vector128.AddSaturate(in2, in2);
            in3 = Vector128.AddSaturate(in3, in3);
            in4 = Vector128.AddSaturate(in4, in4);
            in5 = Vector128.AddSaturate(in5, in5);
            in6 = Vector128.AddSaturate(in6, in6);
            in7 = Vector128.AddSaturate(in7, in7);
        }
    }
}
