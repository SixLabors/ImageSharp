// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Register-resident sixteen-by-sixteen inverse transform kernels for eight-bit blocks.
/// </content>
internal static partial class Av1InverseTransformer
{
    /// <summary>
    /// The scale of the sixteen-point identity transform, two times the square root of two in fixed point.
    /// </summary>
    private const short Identity16Scale = 2 * Av1Transform1dMath.NewSqrt2;

    /// <summary>
    /// Gets the last axis position of each group of sixteen scan positions, for the identity axes.
    /// </summary>
    private static ReadOnlySpan<byte> EndOfBlockFill => [0, 7, 7, 7, 7, 7, 7, 7, 15, 15, 15, 15, 15, 15, 15, 15];

    /// <summary>
    /// Adds the inverse transform of a sixteen-by-sixteen eight-bit block to its prediction with every stage in registers.
    /// </summary>
    /// <remarks>
    /// The end of block selects the one-, eight- or sixteen-coefficient kernel of each axis. The identity axes take dedicated paths that fold the adjacent
    /// stage shift into each identity scale. The stage shifts are -2 and -4.
    /// </remarks>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="transformType">The compound transform type.</param>
    /// <param name="endOfBlock">The one-based final nonzero scan position.</param>
    public static void Inverse16x16(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        Av1TransformType transformType,
        int endOfBlock)
    {
        Av1Transform2dFlipConfiguration.GetAxes(transformType, out Av1TransformType1d columnType, out Av1TransformType1d rowType, out bool flipUpsideDown, out bool flipLeftToRight);
        bool rowIdentity = rowType == Av1TransformType1d.Identity;
        bool columnIdentity = columnType == Av1TransformType1d.Identity;
        if (rowIdentity && columnIdentity)
        {
            InverseIdentity16x16(coefficients, prediction, predictionStride, destination, destinationStride);
            return;
        }

        if (rowIdentity)
        {
            // With a horizontal identity, the vertical extent follows the row of the final coefficient.
            int variant = GetVariant(EndOfBlockFill[(endOfBlock - 1) >> 4]);
            switch (columnType, variant)
            {
                case (Av1TransformType1d.Dct, 0):
                    InverseRowIdentity16x16<Dct16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
                case (Av1TransformType1d.Dct, 1):
                    InverseRowIdentity16x16<Dct16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
                case (Av1TransformType1d.Dct, _):
                    InverseRowIdentity16x16<Dct16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
                case (_, 0):
                    InverseRowIdentity16x16<Adst16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
                case (_, 1):
                    InverseRowIdentity16x16<Adst16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
                default:
                    InverseRowIdentity16x16<Adst16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown);
                    break;
            }

            return;
        }

        if (columnIdentity)
        {
            // With a vertical identity, the horizontal extent follows the row of the final coefficient.
            int variant = GetVariant(EndOfBlockFill[(endOfBlock - 1) >> 4]);
            switch (rowType, variant)
            {
                case (Av1TransformType1d.Dct, 0):
                    InverseColumnIdentity16x16<Dct16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
                case (Av1TransformType1d.Dct, 1):
                    InverseColumnIdentity16x16<Dct16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
                case (Av1TransformType1d.Dct, _):
                    InverseColumnIdentity16x16<Dct16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
                case (_, 0):
                    InverseColumnIdentity16x16<Adst16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
                case (_, 1):
                    InverseColumnIdentity16x16<Adst16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
                default:
                    InverseColumnIdentity16x16<Adst16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipLeftToRight);
                    break;
            }

            return;
        }

        // With the default scan, one coefficient keeps the DC kernels. An end position up to 32 keeps the eight-coefficient kernels. Later positions need all
        // sixteen.
        int shared = endOfBlock == 1 ? 0 : endOfBlock <= 32 ? 1 : 2;
        bool rowDct = rowType == Av1TransformType1d.Dct;
        bool columnDct = columnType == Av1TransformType1d.Dct;
        switch (shared)
        {
            case 0:
                if (rowDct)
                {
                    InverseColumn16x16<Dct16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    InverseColumn16x16<Adst16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }

                break;
            case 1:
                if (rowDct)
                {
                    InverseColumn16x16<Dct16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    InverseColumn16x16<Adst16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }

                break;
            default:
                if (rowDct)
                {
                    InverseColumn16x16<Dct16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    InverseColumn16x16<Adst16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, columnDct, shared, flipUpsideDown, flipLeftToRight);
                }

                break;
        }
    }

    /// <summary>
    /// Maps an axis end position to its sixteen-point kernel variant.
    /// </summary>
    /// <param name="endPosition">The zero-based final nonzero position along the axis.</param>
    /// <returns>Zero for the DC kernel, one for the eight-coefficient kernel, two for the complete kernel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetVariant(int endPosition) => endPosition == 0 ? 0 : endPosition < 8 ? 1 : 2;

    /// <summary>
    /// Selects the vertical kernel of the shared variant and runs the block.
    /// </summary>
    /// <typeparam name="TRow">The horizontal kernel, applied first.</typeparam>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="columnDct">Whether the vertical transform is a DCT. Otherwise it is an ADST.</param>
    /// <param name="variant">Zero for the DC kernel, one for the eight-coefficient kernel, two for the complete kernel.</param>
    /// <param name="flipUpsideDown">Whether the residual block is mirrored vertically.</param>
    /// <param name="flipLeftToRight">Whether the residual block is mirrored horizontally.</param>
    private static void InverseColumn16x16<TRow>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool columnDct,
        int variant,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel16
    {
        switch (variant)
        {
            case 0:
                if (columnDct)
                {
                    Inverse16x16<TRow, Dct16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    Inverse16x16<TRow, Adst16DcInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }

                break;
            case 1:
                if (columnDct)
                {
                    Inverse16x16<TRow, Dct16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    Inverse16x16<TRow, Adst16LowInverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }

                break;
            default:
                if (columnDct)
                {
                    Inverse16x16<TRow, Dct16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }
                else
                {
                    Inverse16x16<TRow, Adst16InverseKernel>(coefficients, prediction, predictionStride, destination, destinationStride, flipUpsideDown, flipLeftToRight);
                }

                break;
        }
    }

    /// <summary>
    /// Applies one horizontal and one vertical kernel to a sixteen-by-sixteen block and adds the result.
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
    private static void Inverse16x16<TRow, TColumn>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool flipUpsideDown,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel16
        where TColumn : struct, IAv1Kernel16
    {
        Load16x16(coefficients, out Vector256<short> r0, out Vector256<short> r1, out Vector256<short> r2, out Vector256<short> r3, out Vector256<short> r4, out Vector256<short> r5, out Vector256<short> r6, out Vector256<short> r7, out Vector256<short> r8, out Vector256<short> r9, out Vector256<short> r10, out Vector256<short> r11, out Vector256<short> r12, out Vector256<short> r13, out Vector256<short> r14, out Vector256<short> r15);

        // Lanes become vertical frequencies so the horizontal transform combines the sixteen column vectors.
        Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        TRow.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, InverseCosBit);
        RoundShift16x16(2, ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);

        // Each vector is now one spatial column. The reversed order mirrors the block horizontally. After that, row k is in r(15 - k), and the vertical kernel
        // runs over the reversed references.
        if (flipLeftToRight)
        {
            Av1TransformKernels.Transpose16x16(ref r15, ref r14, ref r13, ref r12, ref r11, ref r10, ref r9, ref r8, ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0);
            TColumn.Transform(ref r15, ref r14, ref r13, ref r12, ref r11, ref r10, ref r9, ref r8, ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0, InverseCosBit);
        }
        else
        {
            Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
            TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, InverseCosBit);
        }

        RoundShift16x16(4, ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);

        // A horizontal flip leaves row k in r(15 - k), and a vertical flip reverses the row order again. The rows are reversed when exactly one flip applies.
        AddRows16x16(prediction, predictionStride, destination, destinationStride, flipUpsideDown != flipLeftToRight, r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15);
    }

    /// <summary>
    /// Reconstructs a block whose horizontal transform is the identity.
    /// </summary>
    /// <remarks>
    /// The identity scale is elementwise, so the rows never need the transpose. After the scale, each vector is one vertical frequency across the sixteen
    /// columns, which is the input of the vertical kernel.
    /// </remarks>
    /// <typeparam name="TColumn">The vertical kernel.</typeparam>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="flipUpsideDown">Whether the residual block is mirrored vertically.</param>
    private static void InverseRowIdentity16x16<TColumn>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool flipUpsideDown)
        where TColumn : struct, IAv1Kernel16
    {
        Load16x16(coefficients, out Vector256<short> r0, out Vector256<short> r1, out Vector256<short> r2, out Vector256<short> r3, out Vector256<short> r4, out Vector256<short> r5, out Vector256<short> r6, out Vector256<short> r7, out Vector256<short> r8, out Vector256<short> r9, out Vector256<short> r10, out Vector256<short> r11, out Vector256<short> r12, out Vector256<short> r13, out Vector256<short> r14, out Vector256<short> r15);
        ScaleIdentityRows16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        TColumn.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, InverseCosBit);
        RoundShift16x16(4, ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        AddRows16x16(prediction, predictionStride, destination, destinationStride, flipUpsideDown, r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15);
    }

    /// <summary>
    /// Reconstructs a block whose vertical transform is the identity.
    /// </summary>
    /// <typeparam name="TRow">The horizontal kernel.</typeparam>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="flipLeftToRight">Whether the residual block is mirrored horizontally.</param>
    private static void InverseColumnIdentity16x16<TRow>(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool flipLeftToRight)
        where TRow : struct, IAv1Kernel16
    {
        Load16x16(coefficients, out Vector256<short> r0, out Vector256<short> r1, out Vector256<short> r2, out Vector256<short> r3, out Vector256<short> r4, out Vector256<short> r5, out Vector256<short> r6, out Vector256<short> r7, out Vector256<short> r8, out Vector256<short> r9, out Vector256<short> r10, out Vector256<short> r11, out Vector256<short> r12, out Vector256<short> r13, out Vector256<short> r14, out Vector256<short> r15);
        Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        TRow.Transform(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15, InverseCosBit);

        // The first stage shift of -2 adds the rounding offset 2 and shifts right by 2. The addition saturates.
        Vector256<short> half = Vector256.Create((short)2);
        r0 = Vector256.AddSaturate(r0, half) >> 2;
        r1 = Vector256.AddSaturate(r1, half) >> 2;
        r2 = Vector256.AddSaturate(r2, half) >> 2;
        r3 = Vector256.AddSaturate(r3, half) >> 2;
        r4 = Vector256.AddSaturate(r4, half) >> 2;
        r5 = Vector256.AddSaturate(r5, half) >> 2;
        r6 = Vector256.AddSaturate(r6, half) >> 2;
        r7 = Vector256.AddSaturate(r7, half) >> 2;
        r8 = Vector256.AddSaturate(r8, half) >> 2;
        r9 = Vector256.AddSaturate(r9, half) >> 2;
        r10 = Vector256.AddSaturate(r10, half) >> 2;
        r11 = Vector256.AddSaturate(r11, half) >> 2;
        r12 = Vector256.AddSaturate(r12, half) >> 2;
        r13 = Vector256.AddSaturate(r13, half) >> 2;
        r14 = Vector256.AddSaturate(r14, half) >> 2;
        r15 = Vector256.AddSaturate(r15, half) >> 2;

        // The reversed transpose mirrors the lanes horizontally and leaves row k in r(15 - k). The reversed add then restores the row order.
        if (flipLeftToRight)
        {
            Av1TransformKernels.Transpose16x16(ref r15, ref r14, ref r13, ref r12, ref r11, ref r10, ref r9, ref r8, ref r7, ref r6, ref r5, ref r4, ref r3, ref r2, ref r1, ref r0);
        }
        else
        {
            Av1TransformKernels.Transpose16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        }

        AddIdentityRows16x16(prediction, predictionStride, destination, destinationStride, flipLeftToRight, r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15);
    }

    /// <summary>
    /// Reconstructs a block whose two transforms are both the identity.
    /// </summary>
    /// <remarks>
    /// Both scales are elementwise, so no transpose is necessary. An identity transform type has no flips.
    /// </remarks>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    private static void InverseIdentity16x16(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride)
    {
        Load16x16(coefficients, out Vector256<short> r0, out Vector256<short> r1, out Vector256<short> r2, out Vector256<short> r3, out Vector256<short> r4, out Vector256<short> r5, out Vector256<short> r6, out Vector256<short> r7, out Vector256<short> r8, out Vector256<short> r9, out Vector256<short> r10, out Vector256<short> r11, out Vector256<short> r12, out Vector256<short> r13, out Vector256<short> r14, out Vector256<short> r15);
        ScaleIdentityRows16x16(ref r0, ref r1, ref r2, ref r3, ref r4, ref r5, ref r6, ref r7, ref r8, ref r9, ref r10, ref r11, ref r12, ref r13, ref r14, ref r15);
        AddIdentityRows16x16(prediction, predictionStride, destination, destinationStride, false, r0, r1, r2, r3, r4, r5, r6, r7, r8, r9, r10, r11, r12, r13, r14, r15);
    }

    /// <summary>
    /// Packs the sixteen coefficient rows to sixteen bits with saturation.
    /// </summary>
    /// <param name="coefficients">The row-major dequantized coefficients.</param>
    /// <param name="r0">Receives coefficient row 0.</param>
    /// <param name="r1">Receives coefficient row 1.</param>
    /// <param name="r2">Receives coefficient row 2.</param>
    /// <param name="r3">Receives coefficient row 3.</param>
    /// <param name="r4">Receives coefficient row 4.</param>
    /// <param name="r5">Receives coefficient row 5.</param>
    /// <param name="r6">Receives coefficient row 6.</param>
    /// <param name="r7">Receives coefficient row 7.</param>
    /// <param name="r8">Receives coefficient row 8.</param>
    /// <param name="r9">Receives coefficient row 9.</param>
    /// <param name="r10">Receives coefficient row 10.</param>
    /// <param name="r11">Receives coefficient row 11.</param>
    /// <param name="r12">Receives coefficient row 12.</param>
    /// <param name="r13">Receives coefficient row 13.</param>
    /// <param name="r14">Receives coefficient row 14.</param>
    /// <param name="r15">Receives coefficient row 15.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Load16x16(
        ReadOnlySpan<int> coefficients,
        out Vector256<short> r0,
        out Vector256<short> r1,
        out Vector256<short> r2,
        out Vector256<short> r3,
        out Vector256<short> r4,
        out Vector256<short> r5,
        out Vector256<short> r6,
        out Vector256<short> r7,
        out Vector256<short> r8,
        out Vector256<short> r9,
        out Vector256<short> r10,
        out Vector256<short> r11,
        out Vector256<short> r12,
        out Vector256<short> r13,
        out Vector256<short> r14,
        out Vector256<short> r15)
    {
        ref int source = ref MemoryMarshal.GetReference(coefficients);
        r0 = Av1TransformKernels.Load16(ref source, 0);
        r1 = Av1TransformKernels.Load16(ref source, 16);
        r2 = Av1TransformKernels.Load16(ref source, 32);
        r3 = Av1TransformKernels.Load16(ref source, 48);
        r4 = Av1TransformKernels.Load16(ref source, 64);
        r5 = Av1TransformKernels.Load16(ref source, 80);
        r6 = Av1TransformKernels.Load16(ref source, 96);
        r7 = Av1TransformKernels.Load16(ref source, 112);
        r8 = Av1TransformKernels.Load16(ref source, 128);
        r9 = Av1TransformKernels.Load16(ref source, 144);
        r10 = Av1TransformKernels.Load16(ref source, 160);
        r11 = Av1TransformKernels.Load16(ref source, 176);
        r12 = Av1TransformKernels.Load16(ref source, 192);
        r13 = Av1TransformKernels.Load16(ref source, 208);
        r14 = Av1TransformKernels.Load16(ref source, 224);
        r15 = Av1TransformKernels.Load16(ref source, 240);
    }

    /// <summary>
    /// Rounds and shifts sixteen vectors right through a rounding high multiply.
    /// </summary>
    /// <param name="bits">The number of bits to shift right.</param>
    /// <param name="r0">Vector 0. Holds the shifted value on return.</param>
    /// <param name="r1">Vector 1. Holds the shifted value on return.</param>
    /// <param name="r2">Vector 2. Holds the shifted value on return.</param>
    /// <param name="r3">Vector 3. Holds the shifted value on return.</param>
    /// <param name="r4">Vector 4. Holds the shifted value on return.</param>
    /// <param name="r5">Vector 5. Holds the shifted value on return.</param>
    /// <param name="r6">Vector 6. Holds the shifted value on return.</param>
    /// <param name="r7">Vector 7. Holds the shifted value on return.</param>
    /// <param name="r8">Vector 8. Holds the shifted value on return.</param>
    /// <param name="r9">Vector 9. Holds the shifted value on return.</param>
    /// <param name="r10">Vector 10. Holds the shifted value on return.</param>
    /// <param name="r11">Vector 11. Holds the shifted value on return.</param>
    /// <param name="r12">Vector 12. Holds the shifted value on return.</param>
    /// <param name="r13">Vector 13. Holds the shifted value on return.</param>
    /// <param name="r14">Vector 14. Holds the shifted value on return.</param>
    /// <param name="r15">Vector 15. Holds the shifted value on return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RoundShift16x16(
        int bits,
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
        r0 = Av1TransformKernels.RoundShiftRightWide(r0, bits);
        r1 = Av1TransformKernels.RoundShiftRightWide(r1, bits);
        r2 = Av1TransformKernels.RoundShiftRightWide(r2, bits);
        r3 = Av1TransformKernels.RoundShiftRightWide(r3, bits);
        r4 = Av1TransformKernels.RoundShiftRightWide(r4, bits);
        r5 = Av1TransformKernels.RoundShiftRightWide(r5, bits);
        r6 = Av1TransformKernels.RoundShiftRightWide(r6, bits);
        r7 = Av1TransformKernels.RoundShiftRightWide(r7, bits);
        r8 = Av1TransformKernels.RoundShiftRightWide(r8, bits);
        r9 = Av1TransformKernels.RoundShiftRightWide(r9, bits);
        r10 = Av1TransformKernels.RoundShiftRightWide(r10, bits);
        r11 = Av1TransformKernels.RoundShiftRightWide(r11, bits);
        r12 = Av1TransformKernels.RoundShiftRightWide(r12, bits);
        r13 = Av1TransformKernels.RoundShiftRightWide(r13, bits);
        r14 = Av1TransformKernels.RoundShiftRightWide(r14, bits);
        r15 = Av1TransformKernels.RoundShiftRightWide(r15, bits);
    }

    /// <summary>
    /// Scales sixteen rows by the identity transform, with the first stage shift folded in.
    /// </summary>
    /// <param name="r0">Coefficient row 0. Holds the scaled row on return.</param>
    /// <param name="r1">Coefficient row 1. Holds the scaled row on return.</param>
    /// <param name="r2">Coefficient row 2. Holds the scaled row on return.</param>
    /// <param name="r3">Coefficient row 3. Holds the scaled row on return.</param>
    /// <param name="r4">Coefficient row 4. Holds the scaled row on return.</param>
    /// <param name="r5">Coefficient row 5. Holds the scaled row on return.</param>
    /// <param name="r6">Coefficient row 6. Holds the scaled row on return.</param>
    /// <param name="r7">Coefficient row 7. Holds the scaled row on return.</param>
    /// <param name="r8">Coefficient row 8. Holds the scaled row on return.</param>
    /// <param name="r9">Coefficient row 9. Holds the scaled row on return.</param>
    /// <param name="r10">Coefficient row 10. Holds the scaled row on return.</param>
    /// <param name="r11">Coefficient row 11. Holds the scaled row on return.</param>
    /// <param name="r12">Coefficient row 12. Holds the scaled row on return.</param>
    /// <param name="r13">Coefficient row 13. Holds the scaled row on return.</param>
    /// <param name="r14">Coefficient row 14. Holds the scaled row on return.</param>
    /// <param name="r15">Coefficient row 15. Holds the scaled row on return.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ScaleIdentityRows16x16(
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
        // The identity rounding (NewSqrt2Bits) and the -2 stage shift add their offsets in one rounding term. Nested floor divisions combine, so one shift by
        // fourteen gives the same result as the two separate rounding shifts.
        const int shift = Av1Transform1dMath.NewSqrt2Bits + 2;
        Vector256<short> one = Vector256.Create((short)1);
        Vector256<short> scale = Av1TransformKernels.PairWide(Identity16Scale, (1 << (Av1Transform1dMath.NewSqrt2Bits - 1)) + (1 << (shift - 1)));
        r0 = ScaleIdentity(r0, one, scale, shift);
        r1 = ScaleIdentity(r1, one, scale, shift);
        r2 = ScaleIdentity(r2, one, scale, shift);
        r3 = ScaleIdentity(r3, one, scale, shift);
        r4 = ScaleIdentity(r4, one, scale, shift);
        r5 = ScaleIdentity(r5, one, scale, shift);
        r6 = ScaleIdentity(r6, one, scale, shift);
        r7 = ScaleIdentity(r7, one, scale, shift);
        r8 = ScaleIdentity(r8, one, scale, shift);
        r9 = ScaleIdentity(r9, one, scale, shift);
        r10 = ScaleIdentity(r10, one, scale, shift);
        r11 = ScaleIdentity(r11, one, scale, shift);
        r12 = ScaleIdentity(r12, one, scale, shift);
        r13 = ScaleIdentity(r13, one, scale, shift);
        r14 = ScaleIdentity(r14, one, scale, shift);
        r15 = ScaleIdentity(r15, one, scale, shift);
    }

    /// <summary>
    /// Scales one vector by the identity weight with a rounding offset paired to each lane.
    /// </summary>
    /// <remarks>
    /// The unpacks pair each value with one, so each multiply-add gives <c>value * weight + offset</c> in 32 bits. The saturating pack returns to sixteen bits.
    /// </remarks>
    /// <param name="value">The values to scale.</param>
    /// <param name="one">A vector of ones that pairs with each value.</param>
    /// <param name="scale">The interleaved pair of the identity weight and the rounding offset.</param>
    /// <param name="shift">The number of bits to shift right after the multiply-add.</param>
    /// <returns>The scaled values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> ScaleIdentity(Vector256<short> value, Vector256<short> one, Vector256<short> scale, int shift)
    {
        Vector256<int> lower = Vector256_.MultiplyAddAdjacent(Vector256_.UnpackLow(value, one), scale) >> shift;
        Vector256<int> upper = Vector256_.MultiplyAddAdjacent(Vector256_.UnpackHigh(value, one), scale) >> shift;
        return Vector256_.PackSignedSaturate(lower, upper);
    }

    /// <summary>
    /// Scales sixteen rows by the vertical identity transform with the second stage shift and adds them to the prediction.
    /// </summary>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="reversed">Whether vector k holds row 15 - k.</param>
    /// <param name="r0">Residual vector 0 before the vertical identity scale.</param>
    /// <param name="r1">Residual vector 1 before the vertical identity scale.</param>
    /// <param name="r2">Residual vector 2 before the vertical identity scale.</param>
    /// <param name="r3">Residual vector 3 before the vertical identity scale.</param>
    /// <param name="r4">Residual vector 4 before the vertical identity scale.</param>
    /// <param name="r5">Residual vector 5 before the vertical identity scale.</param>
    /// <param name="r6">Residual vector 6 before the vertical identity scale.</param>
    /// <param name="r7">Residual vector 7 before the vertical identity scale.</param>
    /// <param name="r8">Residual vector 8 before the vertical identity scale.</param>
    /// <param name="r9">Residual vector 9 before the vertical identity scale.</param>
    /// <param name="r10">Residual vector 10 before the vertical identity scale.</param>
    /// <param name="r11">Residual vector 11 before the vertical identity scale.</param>
    /// <param name="r12">Residual vector 12 before the vertical identity scale.</param>
    /// <param name="r13">Residual vector 13 before the vertical identity scale.</param>
    /// <param name="r14">Residual vector 14 before the vertical identity scale.</param>
    /// <param name="r15">Residual vector 15 before the vertical identity scale.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddIdentityRows16x16(
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool reversed,
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
        Vector256<short> r15)
    {
        Vector256<short> one = Vector256.Create((short)1);
        Vector256<short> scale = Av1TransformKernels.PairWide(Identity16Scale, 1 << (Av1Transform1dMath.NewSqrt2Bits - 1));
        AddRows16x16(prediction, predictionStride, destination, destinationStride, reversed, ScaleIdentityColumn(r0, one, scale), ScaleIdentityColumn(r1, one, scale), ScaleIdentityColumn(r2, one, scale), ScaleIdentityColumn(r3, one, scale), ScaleIdentityColumn(r4, one, scale), ScaleIdentityColumn(r5, one, scale), ScaleIdentityColumn(r6, one, scale), ScaleIdentityColumn(r7, one, scale), ScaleIdentityColumn(r8, one, scale), ScaleIdentityColumn(r9, one, scale), ScaleIdentityColumn(r10, one, scale), ScaleIdentityColumn(r11, one, scale), ScaleIdentityColumn(r12, one, scale), ScaleIdentityColumn(r13, one, scale), ScaleIdentityColumn(r14, one, scale), ScaleIdentityColumn(r15, one, scale));
    }

    /// <summary>
    /// Scales one vector by the identity weight, then rounds and shifts it right by four in thirty-two bits.
    /// </summary>
    /// <remarks>
    /// The identity scale rounds and shifts by NewSqrt2Bits first. Then the second stage shift rounds and shifts by four.
    /// </remarks>
    /// <param name="value">The values to scale.</param>
    /// <param name="one">A vector of ones that pairs with each value.</param>
    /// <param name="scale">The interleaved pair of the identity weight and the identity rounding offset.</param>
    /// <returns>The scaled and shifted residuals.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<short> ScaleIdentityColumn(Vector256<short> value, Vector256<short> one, Vector256<short> scale)
    {
        Vector256<int> rounding = Vector256.Create(1 << 3);
        Vector256<int> lower = ((Vector256_.MultiplyAddAdjacent(Vector256_.UnpackLow(value, one), scale) >> Av1Transform1dMath.NewSqrt2Bits) + rounding) >> 4;
        Vector256<int> upper = ((Vector256_.MultiplyAddAdjacent(Vector256_.UnpackHigh(value, one), scale) >> Av1Transform1dMath.NewSqrt2Bits) + rounding) >> 4;
        return Vector256_.PackSignedSaturate(lower, upper);
    }

    /// <summary>
    /// Adds sixteen residual rows to the prediction rows, in reverse order when <paramref name="reversed"/> is set.
    /// </summary>
    /// <param name="prediction">The predicted samples.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The reconstructed samples.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="reversed">Whether vector k goes to row 15 - k.</param>
    /// <param name="r0">Residual vector 0.</param>
    /// <param name="r1">Residual vector 1.</param>
    /// <param name="r2">Residual vector 2.</param>
    /// <param name="r3">Residual vector 3.</param>
    /// <param name="r4">Residual vector 4.</param>
    /// <param name="r5">Residual vector 5.</param>
    /// <param name="r6">Residual vector 6.</param>
    /// <param name="r7">Residual vector 7.</param>
    /// <param name="r8">Residual vector 8.</param>
    /// <param name="r9">Residual vector 9.</param>
    /// <param name="r10">Residual vector 10.</param>
    /// <param name="r11">Residual vector 11.</param>
    /// <param name="r12">Residual vector 12.</param>
    /// <param name="r13">Residual vector 13.</param>
    /// <param name="r14">Residual vector 14.</param>
    /// <param name="r15">Residual vector 15.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddRows16x16(
        ReadOnlySpan<byte> prediction,
        int predictionStride,
        Span<byte> destination,
        int destinationStride,
        bool reversed,
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
        Vector256<short> r15)
    {
        ref byte predictionBase = ref MemoryMarshal.GetReference(prediction);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        if (reversed)
        {
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r15);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r14);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r13);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r12);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 4, r11);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 5, r10);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 6, r9);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 7, r8);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 8, r7);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 9, r6);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 10, r5);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 11, r4);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 12, r3);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 13, r2);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 14, r1);
            AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 15, r0);
            return;
        }

        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 0, r0);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 1, r1);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 2, r2);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 3, r3);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 4, r4);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 5, r5);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 6, r6);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 7, r7);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 8, r8);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 9, r9);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 10, r10);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 11, r11);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 12, r12);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 13, r13);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 14, r14);
        AddRow16(ref predictionBase, predictionStride, ref destinationBase, destinationStride, 15, r15);
    }

    /// <summary>
    /// Adds one sixteen-sample residual row to its prediction row with clipping.
    /// </summary>
    /// <param name="prediction">The first predicted sample of the block.</param>
    /// <param name="predictionStride">The number of predicted samples between rows.</param>
    /// <param name="destination">The first reconstructed sample of the block.</param>
    /// <param name="destinationStride">The number of reconstructed samples between rows.</param>
    /// <param name="row">The row index.</param>
    /// <param name="residual">The sixteen residuals of the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddRow16(ref byte prediction, int predictionStride, ref byte destination, int destinationStride, int row, Vector256<short> residual)
        => Av1TransformKernels.AddClip16(
            ref Unsafe.Add(ref prediction, row * predictionStride),
            residual,
            ref Unsafe.Add(ref destination, row * destinationStride));

    /// <summary>
    /// Runs stages five to seven of the sixteen-point inverse DCT.
    /// </summary>
    /// <param name="x0">Stage value 0 from stage four. The method overwrites it.</param>
    /// <param name="x1">Stage value 1 from stage four. The method overwrites it.</param>
    /// <param name="x2">Stage value 2 from stage four. The method overwrites it.</param>
    /// <param name="x3">Stage value 3 from stage four. The method overwrites it.</param>
    /// <param name="x4">Stage value 4 from stage four. The method overwrites it.</param>
    /// <param name="x5">Stage value 5 from stage four. The method overwrites it.</param>
    /// <param name="x6">Stage value 6 from stage four. The method overwrites it.</param>
    /// <param name="x7">Stage value 7 from stage four. The method overwrites it.</param>
    /// <param name="x8">Stage value 8 from stage four. The method overwrites it.</param>
    /// <param name="x9">Stage value 9 from stage four. The method overwrites it.</param>
    /// <param name="x10">Stage value 10 from stage four. The method overwrites it.</param>
    /// <param name="x11">Stage value 11 from stage four. The method overwrites it.</param>
    /// <param name="x12">Stage value 12 from stage four. The method overwrites it.</param>
    /// <param name="x13">Stage value 13 from stage four. The method overwrites it.</param>
    /// <param name="x14">Stage value 14 from stage four. The method overwrites it.</param>
    /// <param name="x15">Stage value 15 from stage four. The method overwrites it.</param>
    /// <param name="out0">Receives output 0.</param>
    /// <param name="out1">Receives output 1.</param>
    /// <param name="out2">Receives output 2.</param>
    /// <param name="out3">Receives output 3.</param>
    /// <param name="out4">Receives output 4.</param>
    /// <param name="out5">Receives output 5.</param>
    /// <param name="out6">Receives output 6.</param>
    /// <param name="out7">Receives output 7.</param>
    /// <param name="out8">Receives output 8.</param>
    /// <param name="out9">Receives output 9.</param>
    /// <param name="out10">Receives output 10.</param>
    /// <param name="out11">Receives output 11.</param>
    /// <param name="out12">Receives output 12.</param>
    /// <param name="out13">Receives output 13.</param>
    /// <param name="out14">Receives output 14.</param>
    /// <param name="out15">Receives output 15.</param>
    /// <param name="c32">The cosine-table weight cos(pi/4).</param>
    /// <param name="rounding">The rounding offset for the widened products.</param>
    /// <param name="cosBit">The number of fractional bits in the weights.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Dct16Stages5To7(
        ref Vector256<short> x0,
        ref Vector256<short> x1,
        ref Vector256<short> x2,
        ref Vector256<short> x3,
        ref Vector256<short> x4,
        ref Vector256<short> x5,
        ref Vector256<short> x6,
        ref Vector256<short> x7,
        ref Vector256<short> x8,
        ref Vector256<short> x9,
        ref Vector256<short> x10,
        ref Vector256<short> x11,
        ref Vector256<short> x12,
        ref Vector256<short> x13,
        ref Vector256<short> x14,
        ref Vector256<short> x15,
        out Vector256<short> out0,
        out Vector256<short> out1,
        out Vector256<short> out2,
        out Vector256<short> out3,
        out Vector256<short> out4,
        out Vector256<short> out5,
        out Vector256<short> out6,
        out Vector256<short> out7,
        out Vector256<short> out8,
        out Vector256<short> out9,
        out Vector256<short> out10,
        out Vector256<short> out11,
        out Vector256<short> out12,
        out Vector256<short> out13,
        out Vector256<short> out14,
        out Vector256<short> out15,
        short c32,
        Vector256<int> rounding,
        int cosBit)
    {
        Vector256<short> m32p32 = Av1TransformKernels.PairWide((short)-c32, c32);
        Vector256<short> p32p32 = Av1TransformKernels.PairWide(c32, c32);

        // Stage 5: x0 to x3 complete their four-point DCT, x5 and x6 take the cos(pi/4) butterfly, and x8 to x15 take saturated sums and differences.
        Av1TransformKernels.AddSubtractWide(ref x0, ref x3);
        Av1TransformKernels.AddSubtractWide(ref x1, ref x2);
        Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x5, ref x6, rounding, cosBit);
        Av1TransformKernels.AddSubtractWide(ref x8, ref x11);
        Av1TransformKernels.AddSubtractWide(ref x9, ref x10);
        Av1TransformKernels.AddSubtractWide(ref x15, ref x12);
        Av1TransformKernels.AddSubtractWide(ref x14, ref x13);

        // Stage 6: x0 to x7 complete their eight-point DCT, and the cos(pi/4) butterflies combine (x10, x13) and (x11, x12).
        Av1TransformKernels.AddSubtractWide(ref x0, ref x7);
        Av1TransformKernels.AddSubtractWide(ref x1, ref x6);
        Av1TransformKernels.AddSubtractWide(ref x2, ref x5);
        Av1TransformKernels.AddSubtractWide(ref x3, ref x4);
        Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x10, ref x13, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(m32p32, p32p32, ref x11, ref x12, rounding, cosBit);

        // Stage 7: the output butterflies combine the even and odd halves with saturation.
        out0 = Vector256.AddSaturate(x0, x15);
        out15 = Vector256.SubtractSaturate(x0, x15);
        out1 = Vector256.AddSaturate(x1, x14);
        out14 = Vector256.SubtractSaturate(x1, x14);
        out2 = Vector256.AddSaturate(x2, x13);
        out13 = Vector256.SubtractSaturate(x2, x13);
        out3 = Vector256.AddSaturate(x3, x12);
        out12 = Vector256.SubtractSaturate(x3, x12);
        out4 = Vector256.AddSaturate(x4, x11);
        out11 = Vector256.SubtractSaturate(x4, x11);
        out5 = Vector256.AddSaturate(x5, x10);
        out10 = Vector256.SubtractSaturate(x5, x10);
        out6 = Vector256.AddSaturate(x6, x9);
        out9 = Vector256.SubtractSaturate(x6, x9);
        out7 = Vector256.AddSaturate(x7, x8);
        out8 = Vector256.SubtractSaturate(x7, x8);
    }

    /// <summary>
    /// Runs stages three to nine of the sixteen-point inverse ADST.
    /// </summary>
    /// <param name="x0">Stage value 0 from stage two. The method overwrites it.</param>
    /// <param name="x1">Stage value 1 from stage two. The method overwrites it.</param>
    /// <param name="x2">Stage value 2 from stage two. The method overwrites it.</param>
    /// <param name="x3">Stage value 3 from stage two. The method overwrites it.</param>
    /// <param name="x4">Stage value 4 from stage two. The method overwrites it.</param>
    /// <param name="x5">Stage value 5 from stage two. The method overwrites it.</param>
    /// <param name="x6">Stage value 6 from stage two. The method overwrites it.</param>
    /// <param name="x7">Stage value 7 from stage two. The method overwrites it.</param>
    /// <param name="x8">Stage value 8 from stage two. The method overwrites it.</param>
    /// <param name="x9">Stage value 9 from stage two. The method overwrites it.</param>
    /// <param name="x10">Stage value 10 from stage two. The method overwrites it.</param>
    /// <param name="x11">Stage value 11 from stage two. The method overwrites it.</param>
    /// <param name="x12">Stage value 12 from stage two. The method overwrites it.</param>
    /// <param name="x13">Stage value 13 from stage two. The method overwrites it.</param>
    /// <param name="x14">Stage value 14 from stage two. The method overwrites it.</param>
    /// <param name="x15">Stage value 15 from stage two. The method overwrites it.</param>
    /// <param name="out0">Receives output 0.</param>
    /// <param name="out1">Receives output 1.</param>
    /// <param name="out2">Receives output 2.</param>
    /// <param name="out3">Receives output 3.</param>
    /// <param name="out4">Receives output 4.</param>
    /// <param name="out5">Receives output 5.</param>
    /// <param name="out6">Receives output 6.</param>
    /// <param name="out7">Receives output 7.</param>
    /// <param name="out8">Receives output 8.</param>
    /// <param name="out9">Receives output 9.</param>
    /// <param name="out10">Receives output 10.</param>
    /// <param name="out11">Receives output 11.</param>
    /// <param name="out12">Receives output 12.</param>
    /// <param name="out13">Receives output 13.</param>
    /// <param name="out14">Receives output 14.</param>
    /// <param name="out15">Receives output 15.</param>
    /// <param name="cospi">The cosine table for <paramref name="cosBit"/>.</param>
    /// <param name="rounding">The rounding offset for the widened products.</param>
    /// <param name="cosBit">The number of fractional bits in the weights.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Adst16Stages3To9(
        ref Vector256<short> x0,
        ref Vector256<short> x1,
        ref Vector256<short> x2,
        ref Vector256<short> x3,
        ref Vector256<short> x4,
        ref Vector256<short> x5,
        ref Vector256<short> x6,
        ref Vector256<short> x7,
        ref Vector256<short> x8,
        ref Vector256<short> x9,
        ref Vector256<short> x10,
        ref Vector256<short> x11,
        ref Vector256<short> x12,
        ref Vector256<short> x13,
        ref Vector256<short> x14,
        ref Vector256<short> x15,
        out Vector256<short> out0,
        out Vector256<short> out1,
        out Vector256<short> out2,
        out Vector256<short> out3,
        out Vector256<short> out4,
        out Vector256<short> out5,
        out Vector256<short> out6,
        out Vector256<short> out7,
        out Vector256<short> out8,
        out Vector256<short> out9,
        out Vector256<short> out10,
        out Vector256<short> out11,
        out Vector256<short> out12,
        out Vector256<short> out13,
        out Vector256<short> out14,
        out Vector256<short> out15,
        ReadOnlySpan<int> cospi,
        Vector256<int> rounding,
        int cosBit)
    {
        short c8 = (short)cospi[8];
        short c56 = (short)cospi[56];
        short c40 = (short)cospi[40];
        short c24 = (short)cospi[24];
        short c16 = (short)cospi[16];
        short c48 = (short)cospi[48];
        Vector256<short> p08p56 = Av1TransformKernels.PairWide(c8, c56);
        Vector256<short> p40p24 = Av1TransformKernels.PairWide(c40, c24);
        Vector256<short> p16p48 = Av1TransformKernels.PairWide(c16, c48);
        Vector256<short> p48m16 = Av1TransformKernels.PairWide(c48, (short)-c16);
        Vector256<short> m48p16 = Av1TransformKernels.PairWide((short)-c48, c16);

        // Stage 3: saturated sums and differences between the two halves.
        Av1TransformKernels.AddSubtractWide(ref x0, ref x8);
        Av1TransformKernels.AddSubtractWide(ref x1, ref x9);
        Av1TransformKernels.AddSubtractWide(ref x2, ref x10);
        Av1TransformKernels.AddSubtractWide(ref x3, ref x11);
        Av1TransformKernels.AddSubtractWide(ref x4, ref x12);
        Av1TransformKernels.AddSubtractWide(ref x5, ref x13);
        Av1TransformKernels.AddSubtractWide(ref x6, ref x14);
        Av1TransformKernels.AddSubtractWide(ref x7, ref x15);

        // Stage 4: rotate x8 to x15 with the cosine-table weights 8 and 56, and 40 and 24.
        Av1TransformKernels.ButterflyWide(p08p56, Av1TransformKernels.PairWide(c56, (short)-c8), ref x8, ref x9, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(p40p24, Av1TransformKernels.PairWide(c24, (short)-c40), ref x10, ref x11, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c56, c8), p08p56, ref x12, ref x13, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c24, c40), p40p24, ref x14, ref x15, rounding, cosBit);

        // Stage 5: saturated sums and differences between the quarters of each half.
        Av1TransformKernels.AddSubtractWide(ref x0, ref x4);
        Av1TransformKernels.AddSubtractWide(ref x1, ref x5);
        Av1TransformKernels.AddSubtractWide(ref x2, ref x6);
        Av1TransformKernels.AddSubtractWide(ref x3, ref x7);
        Av1TransformKernels.AddSubtractWide(ref x8, ref x12);
        Av1TransformKernels.AddSubtractWide(ref x9, ref x13);
        Av1TransformKernels.AddSubtractWide(ref x10, ref x14);
        Av1TransformKernels.AddSubtractWide(ref x11, ref x15);

        // Stage 6: rotate the second quarter of each half with the weights 16 and 48.
        Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x4, ref x5, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(m48p16, p16p48, ref x6, ref x7, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x12, ref x13, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(m48p16, p16p48, ref x14, ref x15, rounding, cosBit);

        // Stage 7: saturated sums and differences between value pairs two positions apart.
        Av1TransformKernels.AddSubtractWide(ref x0, ref x2);
        Av1TransformKernels.AddSubtractWide(ref x1, ref x3);
        Av1TransformKernels.AddSubtractWide(ref x4, ref x6);
        Av1TransformKernels.AddSubtractWide(ref x5, ref x7);
        Av1TransformKernels.AddSubtractWide(ref x8, ref x10);
        Av1TransformKernels.AddSubtractWide(ref x9, ref x11);
        Av1TransformKernels.AddSubtractWide(ref x12, ref x14);
        Av1TransformKernels.AddSubtractWide(ref x13, ref x15);

        Adst16Stages8And9(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out out0, out out1, out out2, out out3, out out4, out out5, out out6, out out7, out out8, out out9, out out10, out out11, out out12, out out13, out out14, out out15, (short)cospi[32], rounding, cosBit);
    }

    /// <summary>
    /// Runs stages eight and nine of the sixteen-point inverse ADST.
    /// </summary>
    /// <param name="x0">Stage value 0 from stage seven. The method can overwrite it.</param>
    /// <param name="x1">Stage value 1 from stage seven. The method can overwrite it.</param>
    /// <param name="x2">Stage value 2 from stage seven. The method can overwrite it.</param>
    /// <param name="x3">Stage value 3 from stage seven. The method can overwrite it.</param>
    /// <param name="x4">Stage value 4 from stage seven. The method can overwrite it.</param>
    /// <param name="x5">Stage value 5 from stage seven. The method can overwrite it.</param>
    /// <param name="x6">Stage value 6 from stage seven. The method can overwrite it.</param>
    /// <param name="x7">Stage value 7 from stage seven. The method can overwrite it.</param>
    /// <param name="x8">Stage value 8 from stage seven. The method can overwrite it.</param>
    /// <param name="x9">Stage value 9 from stage seven. The method can overwrite it.</param>
    /// <param name="x10">Stage value 10 from stage seven. The method can overwrite it.</param>
    /// <param name="x11">Stage value 11 from stage seven. The method can overwrite it.</param>
    /// <param name="x12">Stage value 12 from stage seven. The method can overwrite it.</param>
    /// <param name="x13">Stage value 13 from stage seven. The method can overwrite it.</param>
    /// <param name="x14">Stage value 14 from stage seven. The method can overwrite it.</param>
    /// <param name="x15">Stage value 15 from stage seven. The method can overwrite it.</param>
    /// <param name="out0">Receives output 0.</param>
    /// <param name="out1">Receives output 1.</param>
    /// <param name="out2">Receives output 2.</param>
    /// <param name="out3">Receives output 3.</param>
    /// <param name="out4">Receives output 4.</param>
    /// <param name="out5">Receives output 5.</param>
    /// <param name="out6">Receives output 6.</param>
    /// <param name="out7">Receives output 7.</param>
    /// <param name="out8">Receives output 8.</param>
    /// <param name="out9">Receives output 9.</param>
    /// <param name="out10">Receives output 10.</param>
    /// <param name="out11">Receives output 11.</param>
    /// <param name="out12">Receives output 12.</param>
    /// <param name="out13">Receives output 13.</param>
    /// <param name="out14">Receives output 14.</param>
    /// <param name="out15">Receives output 15.</param>
    /// <param name="c32">The cosine-table weight cos(pi/4).</param>
    /// <param name="rounding">The rounding offset for the widened products.</param>
    /// <param name="cosBit">The number of fractional bits in the weights.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Adst16Stages8And9(
        ref Vector256<short> x0,
        ref Vector256<short> x1,
        ref Vector256<short> x2,
        ref Vector256<short> x3,
        ref Vector256<short> x4,
        ref Vector256<short> x5,
        ref Vector256<short> x6,
        ref Vector256<short> x7,
        ref Vector256<short> x8,
        ref Vector256<short> x9,
        ref Vector256<short> x10,
        ref Vector256<short> x11,
        ref Vector256<short> x12,
        ref Vector256<short> x13,
        ref Vector256<short> x14,
        ref Vector256<short> x15,
        out Vector256<short> out0,
        out Vector256<short> out1,
        out Vector256<short> out2,
        out Vector256<short> out3,
        out Vector256<short> out4,
        out Vector256<short> out5,
        out Vector256<short> out6,
        out Vector256<short> out7,
        out Vector256<short> out8,
        out Vector256<short> out9,
        out Vector256<short> out10,
        out Vector256<short> out11,
        out Vector256<short> out12,
        out Vector256<short> out13,
        out Vector256<short> out14,
        out Vector256<short> out15,
        short c32,
        Vector256<int> rounding,
        int cosBit)
    {
        Vector256<short> p32p32 = Av1TransformKernels.PairWide(c32, c32);
        Vector256<short> p32m32 = Av1TransformKernels.PairWide(c32, (short)-c32);
        Vector256<short> zero = Vector256<short>.Zero;

        // Stage 8: the cos(pi/4) butterflies.
        Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x2, ref x3, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x6, ref x7, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x10, ref x11, rounding, cosBit);
        Av1TransformKernels.ButterflyWide(p32p32, p32m32, ref x14, ref x15, rounding, cosBit);

        // Stage 9: the ADST output permutation, which negates every odd output with saturation.
        out0 = x0;
        out1 = Vector256.SubtractSaturate(zero, x8);
        out2 = x12;
        out3 = Vector256.SubtractSaturate(zero, x4);
        out4 = x6;
        out5 = Vector256.SubtractSaturate(zero, x14);
        out6 = x10;
        out7 = Vector256.SubtractSaturate(zero, x2);
        out8 = x3;
        out9 = Vector256.SubtractSaturate(zero, x11);
        out10 = x15;
        out11 = Vector256.SubtractSaturate(zero, x7);
        out12 = x5;
        out13 = Vector256.SubtractSaturate(zero, x13);
        out14 = x9;
        out15 = Vector256.SubtractSaturate(zero, x1);
    }

    /// <summary>
    /// The sixteen-point inverse DCT on sixteen lanes.
    /// </summary>
    private readonly struct Dct16InverseKernel : IAv1Kernel16
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
            short c4 = (short)cospi[4];
            short c60 = (short)cospi[60];
            short c36 = (short)cospi[36];
            short c28 = (short)cospi[28];
            short c20 = (short)cospi[20];
            short c44 = (short)cospi[44];
            short c52 = (short)cospi[52];
            short c12 = (short)cospi[12];
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c40 = (short)cospi[40];
            short c24 = (short)cospi[24];
            short c32 = (short)cospi[32];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];

            // Stage 1: bit-reversed input order. The even inputs feed the eight-point DCT half, and the odd inputs feed the rotation half.
            Vector256<short> x0 = in0;
            Vector256<short> x1 = in8;
            Vector256<short> x2 = in4;
            Vector256<short> x3 = in12;
            Vector256<short> x4 = in2;
            Vector256<short> x5 = in10;
            Vector256<short> x6 = in6;
            Vector256<short> x7 = in14;
            Vector256<short> x8 = in1;
            Vector256<short> x9 = in9;
            Vector256<short> x10 = in5;
            Vector256<short> x11 = in13;
            Vector256<short> x12 = in3;
            Vector256<short> x13 = in11;
            Vector256<short> x14 = in7;
            Vector256<short> x15 = in15;

            // Stage 2: rotate the odd pairs (x8, x15), (x9, x14), (x10, x13) and (x11, x12).
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c60, (short)-c4), Av1TransformKernels.PairWide(c4, c60), ref x8, ref x15, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c28, (short)-c36), Av1TransformKernels.PairWide(c36, c28), ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c44, (short)-c20), Av1TransformKernels.PairWide(c20, c44), ref x10, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c12, (short)-c52), Av1TransformKernels.PairWide(c52, c12), ref x11, ref x12, rounding, cosBit);

            // Stage 3: rotate (x4, x7) and (x5, x6), and take saturated sums and differences of x8 to x15.
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c56, (short)-c8), Av1TransformKernels.PairWide(c8, c56), ref x4, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c24, (short)-c40), Av1TransformKernels.PairWide(c40, c24), ref x5, ref x6, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x14);

            // Stage 4: the cos(pi/4) butterfly of (x0, x1), the rotation of (x2, x3), sums and differences of x4 to x7, and rotations of x9, x10, x13, x14.
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c32, c32), Av1TransformKernels.PairWide(c32, (short)-c32), ref x0, ref x1, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c48, (short)-c16), Av1TransformKernels.PairWide(c16, c48), ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x6);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c16, c48), Av1TransformKernels.PairWide(c48, c16), ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c48, (short)-c16), Av1TransformKernels.PairWide((short)-c16, c48), ref x10, ref x13, rounding, cosBit);

            Dct16Stages5To7(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out in0, out in1, out in2, out in3, out in4, out in5, out in6, out in7, out in8, out in9, out in10, out in11, out in12, out in13, out in14, out in15, c32, rounding, cosBit);
        }
    }

    /// <summary>
    /// The sixteen-point inverse DCT on sixteen lanes, for blocks whose axis ends within eight coefficients.
    /// </summary>
    private readonly struct Dct16LowInverseKernel : IAv1Kernel16
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

            // Inputs 8 to 15 are zero, so stage 1 places only the nonzero inputs in bit-reversed order. Each stage-2 and stage-3 rotation then has one zero
            // input and reduces to two scaled copies of the other input.
            Vector256<short> x0 = in0;
            Vector256<short> x2 = in4;
            Vector256<short> x4 = in2;
            Vector256<short> x6 = in6;
            Vector256<short> x8 = in1;
            Vector256<short> x10 = in5;
            Vector256<short> x12 = in3;
            Vector256<short> x14 = in7;

            // Stage 2: the odd rotations with one zero input.
            Av1TransformKernels.ScaleWide((short)cospi[60], (short)cospi[4], x8, out x8, out Vector256<short> x15);
            Av1TransformKernels.ScaleWide((short)-cospi[36], (short)cospi[28], x14, out Vector256<short> x9, out x14);
            Av1TransformKernels.ScaleWide((short)cospi[44], (short)cospi[20], x10, out x10, out Vector256<short> x13);
            Av1TransformKernels.ScaleWide((short)-cospi[52], (short)cospi[12], x12, out Vector256<short> x11, out x12);

            // Stage 3: the rotations of x4 and x6 with one zero input, and saturated sums and differences of x8 to x15.
            Av1TransformKernels.ScaleWide((short)cospi[56], (short)cospi[8], x4, out x4, out Vector256<short> x7);
            Av1TransformKernels.ScaleWide((short)-cospi[40], (short)cospi[24], x6, out Vector256<short> x5, out x6);
            Av1TransformKernels.AddSubtractWide(ref x8, ref x9);
            Av1TransformKernels.AddSubtractWide(ref x11, ref x10);
            Av1TransformKernels.AddSubtractWide(ref x12, ref x13);
            Av1TransformKernels.AddSubtractWide(ref x15, ref x14);

            // Stage 4: x1 and x3 are zero, so the butterflies of (x0, x1) and (x2, x3) reduce to scaled copies. The other operations match the complete kernel.
            Av1TransformKernels.ScaleWide(c32, c32, x0, out x0, out Vector256<short> x1);
            Av1TransformKernels.ScaleWide(c48, c16, x2, out x2, out Vector256<short> x3);
            Av1TransformKernels.AddSubtractWide(ref x4, ref x5);
            Av1TransformKernels.AddSubtractWide(ref x7, ref x6);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c16, c48), Av1TransformKernels.PairWide(c48, c16), ref x9, ref x14, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide((short)-c48, (short)-c16), Av1TransformKernels.PairWide((short)-c16, c48), ref x10, ref x13, rounding, cosBit);

            Dct16Stages5To7(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out in0, out in1, out in2, out in3, out in4, out in5, out in6, out in7, out in8, out in9, out in10, out in11, out in12, out in13, out in14, out in15, c32, rounding, cosBit);
        }
    }

    /// <summary>
    /// The DC-only sixteen-point inverse DCT on sixteen lanes.
    /// </summary>
    private readonly struct Dct16DcInverseKernel : IAv1Kernel16
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
            // Only in0 is nonzero, so every output equals in0 times cos(pi/4).
            short c32 = (short)Av1SinusConstants.CosinusPi(cosBit)[32];
            Av1TransformKernels.ScaleWide(c32, c32, in0, out Vector256<short> x0, out Vector256<short> x1);
            in0 = x0;
            in1 = x1;
            in2 = x1;
            in3 = x0;
            in4 = x0;
            in5 = x1;
            in6 = x1;
            in7 = x0;
            in8 = x0;
            in9 = x1;
            in10 = x1;
            in11 = x0;
            in12 = x0;
            in13 = x1;
            in14 = x1;
            in15 = x0;
        }
    }

    /// <summary>
    /// The sixteen-point inverse ADST on sixteen lanes.
    /// </summary>
    private readonly struct Adst16InverseKernel : IAv1Kernel16
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

            // Stage 1: the ADST input permutation.
            Vector256<short> x0 = in15;
            Vector256<short> x1 = in0;
            Vector256<short> x2 = in13;
            Vector256<short> x3 = in2;
            Vector256<short> x4 = in11;
            Vector256<short> x5 = in4;
            Vector256<short> x6 = in9;
            Vector256<short> x7 = in6;
            Vector256<short> x8 = in7;
            Vector256<short> x9 = in8;
            Vector256<short> x10 = in5;
            Vector256<short> x11 = in10;
            Vector256<short> x12 = in3;
            Vector256<short> x13 = in12;
            Vector256<short> x14 = in1;
            Vector256<short> x15 = in14;

            // Stage 2: eight rotations with the odd cosine-table weights.
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c2, c62), Av1TransformKernels.PairWide(c62, (short)-c2), ref x0, ref x1, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c10, c54), Av1TransformKernels.PairWide(c54, (short)-c10), ref x2, ref x3, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c18, c46), Av1TransformKernels.PairWide(c46, (short)-c18), ref x4, ref x5, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c26, c38), Av1TransformKernels.PairWide(c38, (short)-c26), ref x6, ref x7, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c34, c30), Av1TransformKernels.PairWide(c30, (short)-c34), ref x8, ref x9, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c42, c22), Av1TransformKernels.PairWide(c22, (short)-c42), ref x10, ref x11, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c50, c14), Av1TransformKernels.PairWide(c14, (short)-c50), ref x12, ref x13, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(Av1TransformKernels.PairWide(c58, c6), Av1TransformKernels.PairWide(c6, (short)-c58), ref x14, ref x15, rounding, cosBit);

            Adst16Stages3To9(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out in0, out in1, out in2, out in3, out in4, out in5, out in6, out in7, out in8, out in9, out in10, out in11, out in12, out in13, out in14, out in15, cospi, rounding, cosBit);
        }
    }

    /// <summary>
    /// The sixteen-point inverse ADST on sixteen lanes, for blocks whose axis ends within eight coefficients.
    /// </summary>
    private readonly struct Adst16LowInverseKernel : IAv1Kernel16
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

            // Stages 1 and 2: inputs 8 to 15 are zero, so each stage-2 rotation has one zero input. Each rotation reduces to two scaled copies of the input
            // that the ADST permutation places in the pair.
            Av1TransformKernels.ScaleWide((short)cospi[62], (short)-cospi[2], in0, out Vector256<short> x0, out Vector256<short> x1);
            Av1TransformKernels.ScaleWide((short)cospi[54], (short)-cospi[10], in2, out Vector256<short> x2, out Vector256<short> x3);
            Av1TransformKernels.ScaleWide((short)cospi[46], (short)-cospi[18], in4, out Vector256<short> x4, out Vector256<short> x5);
            Av1TransformKernels.ScaleWide((short)cospi[38], (short)-cospi[26], in6, out Vector256<short> x6, out Vector256<short> x7);
            Av1TransformKernels.ScaleWide((short)cospi[34], (short)cospi[30], in7, out Vector256<short> x8, out Vector256<short> x9);
            Av1TransformKernels.ScaleWide((short)cospi[42], (short)cospi[22], in5, out Vector256<short> x10, out Vector256<short> x11);
            Av1TransformKernels.ScaleWide((short)cospi[50], (short)cospi[14], in3, out Vector256<short> x12, out Vector256<short> x13);
            Av1TransformKernels.ScaleWide((short)cospi[58], (short)cospi[6], in1, out Vector256<short> x14, out Vector256<short> x15);

            Adst16Stages3To9(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out in0, out in1, out in2, out in3, out in4, out in5, out in6, out in7, out in8, out in9, out in10, out in11, out in12, out in13, out in14, out in15, cospi, rounding, cosBit);
        }
    }

    /// <summary>
    /// The DC-only sixteen-point inverse ADST on sixteen lanes.
    /// </summary>
    private readonly struct Adst16DcInverseKernel : IAv1Kernel16
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
            short c8 = (short)cospi[8];
            short c56 = (short)cospi[56];
            short c16 = (short)cospi[16];
            short c48 = (short)cospi[48];
            Vector256<short> p08p56 = Av1TransformKernels.PairWide(c8, c56);
            Vector256<short> p56m08 = Av1TransformKernels.PairWide(c56, (short)-c8);
            Vector256<short> p16p48 = Av1TransformKernels.PairWide(c16, c48);
            Vector256<short> p48m16 = Av1TransformKernels.PairWide(c48, (short)-c16);

            // Only in0 is nonzero, so stages 1 and 2 reduce to the input rotation with the cosine-table weights 62 and -2. Each later add-subtract stage copies
            // its nonzero inputs to the zero positions, so those stages become plain copies.
            Av1TransformKernels.ScaleWide((short)cospi[62], (short)-cospi[2], in0, out Vector256<short> x0, out Vector256<short> x1);

            // Stages 3 and 4: x8 and x9 start as copies of x0 and x1 and take the rotation with weights 8 and 56.
            Vector256<short> x8 = x0;
            Vector256<short> x9 = x1;
            Av1TransformKernels.ButterflyWide(p08p56, p56m08, ref x8, ref x9, rounding, cosBit);

            // Stages 5 and 6: (x4, x5) and (x12, x13) start as copies of (x0, x1) and (x8, x9) and take the rotation with weights 16 and 48.
            Vector256<short> x4 = x0;
            Vector256<short> x5 = x1;
            Vector256<short> x12 = x8;
            Vector256<short> x13 = x9;
            Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x4, ref x5, rounding, cosBit);
            Av1TransformKernels.ButterflyWide(p16p48, p48m16, ref x12, ref x13, rounding, cosBit);

            // Stage 7: each value pair two positions apart starts as a copy of the earlier pair.
            Vector256<short> x2 = x0;
            Vector256<short> x3 = x1;
            Vector256<short> x6 = x4;
            Vector256<short> x7 = x5;
            Vector256<short> x10 = x8;
            Vector256<short> x11 = x9;
            Vector256<short> x14 = x12;
            Vector256<short> x15 = x13;

            Adst16Stages8And9(ref x0, ref x1, ref x2, ref x3, ref x4, ref x5, ref x6, ref x7, ref x8, ref x9, ref x10, ref x11, ref x12, ref x13, ref x14, ref x15, out in0, out in1, out in2, out in3, out in4, out in5, out in6, out in7, out in8, out in9, out in10, out in11, out in12, out in13, out in14, out in15, (short)cospi[32], rounding, cosBit);
        }
    }
}
