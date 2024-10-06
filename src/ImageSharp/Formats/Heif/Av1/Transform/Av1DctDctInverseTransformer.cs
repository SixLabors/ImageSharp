// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal class Av1DctDctInverseTransformer
{
    private const int UnitQuantizationShift = 2;

    internal static void InverseTransformAdd(ref int coefficients, Span<byte> readBuffer, int readStride, Span<byte> writeBuffer, int writeStride, Av1TransformFunctionParameters transformFunctionParameters)
    {
        Guard.IsTrue(transformFunctionParameters.TransformType == Av1TransformType.DctDct, nameof(transformFunctionParameters.TransformType), "This class implements DCT-DCT transformations only.");

        switch (transformFunctionParameters.TransformSize)
        {
            case Av1TransformSize.Size4x4:
                InverseWhalshHadamard4x4(ref coefficients, ref readBuffer[0], readStride, ref writeBuffer[0], writeStride, transformFunctionParameters.EndOfBuffer, transformFunctionParameters.BitDepth);
                break;
            default:
                throw new NotImplementedException("Only 4x4 transformation size supported for now");
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

            destForWrite = ClipPixelAdd(destForRead, a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead), b1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 2) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 2), c1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 3) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 3), d1, bitDepth);

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
            destForWrite = ClipPixelAdd(destForRead, a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead), a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 2) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 2), a1, bitDepth);
            Unsafe.Add(ref destForWrite, strideForWrite * 3) = ClipPixelAdd(Unsafe.Add(ref destForRead, strideForRead * 3), a1, bitDepth);
            ip = ref Unsafe.Add(ref ip, 1);
            destForRead = ref Unsafe.Add(ref destForRead, 1);
            destForWrite = ref Unsafe.Add(ref destForWrite, 1);
        }
    }

    private static ushort ClipPixelAdd(ushort value, int trans, int bitDepth)
        => ClipPixel(value + trans, bitDepth);

    private static ushort ClipPixel(int value, int bitDepth)
        => bitDepth switch
        {
            10 => (ushort)Av1Math.Clamp(value, 0, 1023),
            12 => (ushort)Av1Math.Clamp(value, 0, 4095),
            _ => (ushort)Av1Math.Clamp(value, 0, 255),
        };
}
