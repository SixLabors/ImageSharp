// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1Inverse2dTransformer
{
    private const int UnitQuantizationShift = 2;

    /// <summary>
    /// SVT: inv_txfm2d_add_c
    /// </summary>
    internal static void InverseTransform2dAdd(
        Span<int> input,
        Span<ushort> outputForRead,
        int strideForRead,
        Span<ushort> outputForWrite,
        int strideForWrite,
        Av1Transform2dFlipConfiguration config,
        Span<int> transformFunctionBuffer,
        int bitDepth)
    {
        // Note when assigning txfm_size_col, we use the txfm_size from the
        // row configuration and vice versa. This is intentionally done to
        // accurately perform rectangular transforms. When the transform is
        // rectangular, the number of columns will be the same as the
        // txfm_size stored in the row cfg struct. It will make no difference
        // for square transforms.
        int transformWidth = config.TransformSize.GetWidth();
        int transformHeight = config.TransformSize.GetHeight();

        // Take the shift from the larger dimension in the rectangular case.
        Span<int> shift = config.Shift;
        int rectangleType = config.TransformSize.GetRectangleLogRatio();
        config.GenerateStageRange(bitDepth);

        int cosBitColumn = config.CosBitColumn;
        int cosBitRow = config.CosBitRow;
        IAv1Forward1dTransformer? functionColumn = Av1InverseTransformerFactory.GetTransformer(config.TransformFunctionTypeColumn);
        IAv1Forward1dTransformer? functionRow = Av1InverseTransformerFactory.GetTransformer(config.TransformFunctionTypeRow);
        Guard.NotNull(functionColumn);
        Guard.NotNull(functionRow);

        // txfm_buf's length is  txfm_size_row * txfm_size_col + 2 * MAX(txfm_size_row, txfm_size_col)
        // it is used for intermediate data buffering
        int bufferOffset = Math.Max(transformHeight, transformWidth);
        Guard.MustBeSizedAtLeast(transformFunctionBuffer, (transformHeight * transformWidth) + (2 * bufferOffset), nameof(transformFunctionBuffer));
        Span<int> tempIn = transformFunctionBuffer;
        Span<int> tempOut = tempIn.Slice(bufferOffset);
        Span<int> buf = tempOut.Slice(bufferOffset);
        Span<int> bufPtr = buf;
        int c, r;

        // Rows
        for (r = 0; r < transformHeight; ++r)
        {
            if (Math.Abs(rectangleType) == 1)
            {
                for (c = 0; c < transformWidth; ++c)
                {
                    tempIn[c] = Av1Math.RoundShift((long)input[c] * Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount);
                }

                Av1InverseTransformMath.ClampBuffer(tempIn, transformWidth, (byte)(bitDepth + 8));
                functionRow.Transform(tempIn, bufPtr, cosBitRow, config.StageRangeRow);
            }
            else
            {
                for (c = 0; c < transformWidth; ++c)
                {
                    tempIn[c] = input[c];
                }

                Av1InverseTransformMath.ClampBuffer(tempIn, transformWidth, (byte)(bitDepth + 8));
                functionRow.Transform(tempIn, bufPtr, cosBitRow, config.StageRangeRow);
            }

            Av1InverseTransformMath.RoundShiftArray(bufPtr, transformWidth, -shift[0]);
            input.Slice(transformWidth);
            bufPtr.Slice(transformWidth);
        }

        // Columns
        for (c = 0; c < transformWidth; ++c)
        {
            if (!config.FlipLeftToRight)
            {
                for (r = 0; r < transformHeight; ++r)
                {
                    tempIn[r] = buf[(r * transformWidth) + c];
                }
            }
            else
            {
                // flip left right
                for (r = 0; r < transformHeight; ++r)
                {
                    tempIn[r] = buf[(r * transformWidth) + (transformWidth - c - 1)];
                }
            }

            Av1InverseTransformMath.ClampBuffer(tempIn, transformHeight, (byte)Math.Max(bitDepth + 6, 16));
            functionColumn.Transform(tempIn, tempOut, cosBitColumn, config.StageRangeColumn);
            Av1InverseTransformMath.RoundShiftArray(tempOut, transformHeight, -shift[1]);
            if (!config.FlipUpsideDown)
            {
                for (r = 0; r < transformHeight; ++r)
                {
                    outputForWrite[(r * strideForWrite) + c] =
                        Av1InverseTransformMath.ClipPixelAdd(outputForRead[(r * strideForRead) + c], tempOut[r], bitDepth);
                }
            }
            else
            {
                // flip upside down
                for (r = 0; r < transformHeight; ++r)
                {
                    outputForWrite[(r * strideForWrite) + c] = Av1InverseTransformMath.ClipPixelAdd(
                        outputForRead[(r * strideForRead) + c], tempOut[transformHeight - r - 1], bitDepth);
                }
            }
        }
    }

    /// <summary>
    /// SVT: inv_txfm2d_add_c
    /// </summary>
    internal static void InverseTransform2dAdd(
        Span<int> input,
        Span<byte> outputForRead,
        int strideForRead,
        Span<byte> outputForWrite,
        int strideForWrite,
        Av1Transform2dFlipConfiguration config,
        Span<int> transformFunctionBuffer)
    {
        const int bitDepth = 8;

        // Note when assigning txfm_size_col, we use the txfm_size from the
        // row configuration and vice versa. This is intentionally done to
        // accurately perform rectangular transforms. When the transform is
        // rectangular, the number of columns will be the same as the
        // txfm_size stored in the row cfg struct. It will make no difference
        // for square transforms.
        int transformWidth = config.TransformSize.GetWidth();
        int transformHeight = config.TransformSize.GetHeight();

        // Take the shift from the larger dimension in the rectangular case.
        Span<int> shift = config.Shift;
        int rectangleType = config.TransformSize.GetRectangleLogRatio();
        config.GenerateStageRange(bitDepth);

        int cosBitColumn = config.CosBitColumn;
        int cosBitRow = config.CosBitRow;
        IAv1Forward1dTransformer? functionColumn = Av1InverseTransformerFactory.GetTransformer(config.TransformFunctionTypeColumn);
        IAv1Forward1dTransformer? functionRow = Av1InverseTransformerFactory.GetTransformer(config.TransformFunctionTypeRow);
        Guard.NotNull(functionColumn);
        Guard.NotNull(functionRow);

        // txfm_buf's length is  txfm_size_row * txfm_size_col + 2 * MAX(txfm_size_row, txfm_size_col)
        // it is used for intermediate data buffering
        int bufferOffset = Math.Max(transformHeight, transformWidth);
        Guard.MustBeSizedAtLeast(transformFunctionBuffer, (transformHeight * transformWidth) + (2 * bufferOffset), nameof(transformFunctionBuffer));
        Span<int> tempIn = transformFunctionBuffer;
        Span<int> tempOut = tempIn.Slice(bufferOffset);
        Span<int> buf = tempOut.Slice(bufferOffset);
        Span<int> bufPtr = buf;
        int c, r;

        // Rows
        for (r = 0; r < transformHeight; ++r)
        {
            if (Math.Abs(rectangleType) == 1)
            {
                for (c = 0; c < transformWidth; ++c)
                {
                    tempIn[c] = Av1Math.RoundShift((long)input[c] * Av1InverseTransformMath.NewInverseSqrt2, Av1InverseTransformMath.NewSqrt2BitCount);
                }

                Av1InverseTransformMath.ClampBuffer(tempIn, transformWidth, (byte)(bitDepth + 8));
                functionRow.Transform(tempIn, bufPtr, cosBitRow, config.StageRangeRow);
            }
            else
            {
                for (c = 0; c < transformWidth; ++c)
                {
                    tempIn[c] = input[c];
                }

                Av1InverseTransformMath.ClampBuffer(tempIn, transformWidth, (byte)(bitDepth + 8));
                functionRow.Transform(tempIn, bufPtr, cosBitRow, config.StageRangeRow);
            }

            Av1InverseTransformMath.RoundShiftArray(bufPtr, transformWidth, -shift[0]);
            input.Slice(transformWidth);
            bufPtr.Slice(transformWidth);
        }

        // Columns
        for (c = 0; c < transformWidth; ++c)
        {
            if (!config.FlipLeftToRight)
            {
                for (r = 0; r < transformHeight; ++r)
                {
                    tempIn[r] = buf[(r * transformWidth) + c];
                }
            }
            else
            {
                // flip left right
                for (r = 0; r < transformHeight; ++r)
                {
                    tempIn[r] = buf[(r * transformWidth) + (transformWidth - c - 1)];
                }
            }

            Av1InverseTransformMath.ClampBuffer(tempIn, transformHeight, (byte)Math.Max(bitDepth + 6, 16));
            functionColumn.Transform(tempIn, tempOut, cosBitColumn, config.StageRangeColumn);
            Av1InverseTransformMath.RoundShiftArray(tempOut, transformHeight, -shift[1]);
            if (!config.FlipUpsideDown)
            {
                for (r = 0; r < transformHeight; ++r)
                {
                    outputForWrite[(r * strideForWrite) + c] =
                        Av1InverseTransformMath.ClipPixelAdd(outputForRead[(r * strideForRead) + c], tempOut[r]);
                }
            }
            else
            {
                // flip upside down
                for (r = 0; r < transformHeight; ++r)
                {
                    outputForWrite[(r * strideForWrite) + c] = Av1InverseTransformMath.ClipPixelAdd(
                        outputForRead[(r * strideForRead) + c], tempOut[transformHeight - r - 1]);
                }
            }
        }
    }

    /// <summary>
    /// SVT: highbd_iwht4x4_add
    /// </summary>
    private static void InverseWhalshHadamard4x4(ref int input, ref byte destinationForRead, int strideForRead, ref byte destinationForWrite, int strideForWrite, int endOfBuffer, int bitDepth)
    {
        if (endOfBuffer > 1)
        {
            InverseWhalshHadamard4x4Add16(ref input, ref destinationForRead, strideForRead, ref destinationForWrite, strideForWrite, bitDepth);
        }
        else
        {
            InverseWhalshHadamard4x4Add1(ref input, ref destinationForRead, strideForRead, ref destinationForWrite, strideForWrite, bitDepth);
        }
    }

    /// <summary>
    /// SVT: svt_av1_highbd_iwht4x4_16_add_c
    /// </summary>
    private static void InverseWhalshHadamard4x4Add16(ref int input, ref byte destinationForRead, int strideForRead, ref byte destinationForWrite, int strideForWrite, int bitDepth)
    {
        /* 4-point reversible, orthonormal inverse Walsh-Hadamard in 3.5 adds,
           0.5 shifts per pixel. */
        int i;
        Span<ushort> output = stackalloc ushort[16];
        ushort a1, b1, c1, d1, e1;
        ref int ip = ref input;
        ref ushort op = ref output[0];
        ref ushort opTmp = ref output[0];
        ref ushort destForRead = ref Unsafe.As<byte, ushort>(ref destinationForRead);
        ref ushort destForWrite = ref Unsafe.As<byte, ushort>(ref destinationForWrite);

        for (i = 0; i < 4; i++)
        {
            a1 = (ushort)(ip >> UnitQuantizationShift);
            c1 = (ushort)(Unsafe.Add(ref ip, 1) >> UnitQuantizationShift);
            d1 = (ushort)(Unsafe.Add(ref ip, 2) >> UnitQuantizationShift);
            b1 = (ushort)(Unsafe.Add(ref ip, 3) >> UnitQuantizationShift);
            a1 += c1;
            d1 -= b1;
            e1 = (ushort)((a1 - d1) >> 1);
            b1 = (ushort)(e1 - b1);
            c1 = (ushort)(e1 - c1);
            a1 -= b1;
            d1 += c1;
            op = a1;
            Unsafe.Add(ref op, 1) = b1;
            Unsafe.Add(ref op, 2) = c1;
            Unsafe.Add(ref op, 3) = d1;
            ip = ref Unsafe.Add(ref ip, 4);
            op = ref Unsafe.Add(ref op, 4);
        }

        ip = opTmp;
        for (i = 0; i < 4; i++)
        {
            a1 = (ushort)ip;
            c1 = (ushort)Unsafe.Add(ref ip, 4);
            d1 = (ushort)Unsafe.Add(ref ip, 8);
            b1 = (ushort)Unsafe.Add(ref ip, 12);
            a1 += c1;
            d1 -= b1;
            e1 = (ushort)((a1 - d1) >> 1);
            b1 = (ushort)(e1 - b1);
            c1 = (ushort)(e1 - c1);
            a1 -= b1;
            d1 += c1;
            /* Disabled in normal build
            range_check_value(a1, (int8_t)(bd + 1));
            range_check_value(b1, (int8_t)(bd + 1));
            range_check_value(c1, (int8_t)(bd + 1));
            range_check_value(d1, (int8_t)(bd + 1));
            */

            destForWrite = Av1InverseTransformMath.ClipPixelAdd(destForRead, a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead), b1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 2) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 2), c1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 3) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 3), d1, bitDepth);

            ip = ref Unsafe.Add(ref ip, 1);
            destForRead = ref Unsafe.Add(ref destForRead, 1);
            destForWrite = ref Unsafe.Add(ref destForWrite, 1);
        }
    }

    /// <summary>
    /// SVT: svt_av1_highbd_iwht4x4_1_add_c
    /// </summary>
    private static void InverseWhalshHadamard4x4Add1(ref int input, ref byte destinationForRead, int strideForRead, ref byte destinationForWrite, int strideForWrite, int bitDepth)
    {
        int i;
        ushort a1, e1;
        Span<int> tmp = stackalloc int[4];
        ref int ip = ref input;
        ref int ipTmp = ref tmp[0];
        ref int op = ref tmp[0];
        ref ushort destForRead = ref Unsafe.As<byte, ushort>(ref destinationForRead);
        ref ushort destForWrite = ref Unsafe.As<byte, ushort>(ref destinationForWrite);

        a1 = (ushort)(ip >> UnitQuantizationShift);
        e1 = (ushort)(a1 >> 1);
        a1 -= e1;
        op = a1;
        Unsafe.Add(ref op, 1) = e1;
        Unsafe.Add(ref op, 2) = e1;
        Unsafe.Add(ref op, 3) = e1;

        ip = ipTmp;
        for (i = 0; i < 4; i++)
        {
            e1 = (ushort)(ip >> 1);
            a1 = (ushort)(ip - e1);
            destForWrite = Av1InverseTransformMath.ClipPixelAdd(destForRead, a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead), a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 2) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 2), a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 3) = Av1InverseTransformMath.ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 3), a1, bitDepth);
            ip = ref Unsafe.Add(ref ip, 1);
            destForRead = ref Unsafe.Add(ref destForRead, 1);
            destForWrite = ref Unsafe.Add(ref destForWrite, 1);
        }
    }
}
