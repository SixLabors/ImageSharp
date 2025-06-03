// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// Methods for encoding a VP8 frame.
/// </summary>
internal static unsafe class Vp8Encoding
{
    private const int KC1 = 20091 + (1 << 16);

    private const int KC2 = 35468;

    private static readonly byte[] Clip1 = GetClip1(); // clips [-255,510] to [0,255]

    private const int I16DC16 = 0 * 16 * WebpConstants.Bps;

    private const int I16TM16 = I16DC16 + 16;

    private const int I16VE16 = 1 * 16 * WebpConstants.Bps;

    private const int I16HE16 = I16VE16 + 16;

    private const int C8DC8 = 2 * 16 * WebpConstants.Bps;

    private const int C8TM8 = C8DC8 + (1 * 16);

    private const int C8VE8 = (2 * 16 * WebpConstants.Bps) + (8 * WebpConstants.Bps);

    private const int C8HE8 = C8VE8 + (1 * 16);

    public static readonly int[] Vp8I16ModeOffsets = { I16DC16, I16TM16, I16VE16, I16HE16 };

    public static readonly int[] Vp8UvModeOffsets = { C8DC8, C8TM8, C8VE8, C8HE8 };

    private const int I4DC4 = (3 * 16 * WebpConstants.Bps) + 0;

    private const int I4TM4 = I4DC4 + 4;

    private const int I4VE4 = I4DC4 + 8;

    private const int I4HE4 = I4DC4 + 12;

    private const int I4RD4 = I4DC4 + 16;

    private const int I4VR4 = I4DC4 + 20;

    private const int I4LD4 = I4DC4 + 24;

    private const int I4VL4 = I4DC4 + 28;

    private const int I4HD4 = (3 * 16 * WebpConstants.Bps) + (4 * WebpConstants.Bps);

    private const int I4HU4 = I4HD4 + 4;

    public static readonly int[] Vp8I4ModeOffsets = { I4DC4, I4TM4, I4VE4, I4HE4, I4RD4, I4VR4, I4LD4, I4VL4, I4HD4, I4HU4 };

    private static byte[] GetClip1()
    {
        byte[] clip1 = new byte[255 + 510 + 1];

        for (int i = -255; i <= 255 + 255; i++)
        {
            clip1[255 + i] = Clip8b(i);
        }

        return clip1;
    }

    // Transforms (Paragraph 14.4)
    // Does two inverse transforms.
    public static void ITransformTwo(Span<byte> reference, Span<short> input, Span<byte> dst, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // This implementation makes use of 16-bit fixed point versions of two
            // multiply constants:
            //    K1 = sqrt(2) * cos (pi/8) ~= 85627 / 2^16
            //    K2 = sqrt(2) * sin (pi/8) ~= 35468 / 2^16
            //
            // To be able to use signed 16-bit integers, we use the following trick to
            // have constants within range:
            // - Associated constants are obtained by subtracting the 16-bit fixed point
            //   version of one:
            //      k = K - (1 << 16)  =>  K = k + (1 << 16)
            //      K1 = 85267  =>  k1 =  20091
            //      K2 = 35468  =>  k2 = -30068
            // - The multiplication of a variable by a constant become the sum of the
            //   variable and the multiplication of that variable by the associated
            //   constant:
            //      (x * K) >> 16 = (x * (k + (1 << 16))) >> 16 = ((x * k ) >> 16) + x

            // Load and concatenate the transform coefficients (we'll do two inverse
            // transforms in parallel). In the case of only one inverse transform, the
            // second half of the vectors will just contain random value we'll never
            // use nor store.
            ref short inputRef = ref MemoryMarshal.GetReference(input);
            Vector128<long> in0 = Vector128.Create(Unsafe.As<short, long>(ref inputRef), 0);
            Vector128<long> in1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 4)), 0);
            Vector128<long> in2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 8)), 0);
            Vector128<long> in3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 12)), 0);

            // a00 a10 a20 a30   x x x x
            // a01 a11 a21 a31   x x x x
            // a02 a12 a22 a32   x x x x
            // a03 a13 a23 a33   x x x x
            Vector128<long> inb0 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 16)), 0);
            Vector128<long> inb1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 20)), 0);
            Vector128<long> inb2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 24)), 0);
            Vector128<long> inb3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 28)), 0);

            in0 = Vector128_.UnpackLow(in0, inb0);
            in1 = Vector128_.UnpackLow(in1, inb1);
            in2 = Vector128_.UnpackLow(in2, inb2);
            in3 = Vector128_.UnpackLow(in3, inb3);

            // a00 a10 a20 a30   b00 b10 b20 b30
            // a01 a11 a21 a31   b01 b11 b21 b31
            // a02 a12 a22 a32   b02 b12 b22 b32
            // a03 a13 a23 a33   b03 b13 b23 b33

            // Vertical pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            InverseTransformVerticalPassVector128(in0, in2, in1, in3, out Vector128<short> tmp0, out Vector128<short> tmp1, out Vector128<short> tmp2, out Vector128<short> tmp3);

            // Transpose the two 4x4.
            LossyUtils.Vp8Transpose_2_4x4_16bVector128(tmp0, tmp1, tmp2, tmp3, out Vector128<long> t0, out Vector128<long> t1, out Vector128<long> t2, out Vector128<long> t3);

            // Horizontal pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            InverseTransformHorizontalPassVector128(t0, t2, t1, t3, out Vector128<short> shifted0, out Vector128<short> shifted1, out Vector128<short> shifted2, out Vector128<short> shifted3);

            // Transpose the two 4x4.
            LossyUtils.Vp8Transpose_2_4x4_16bVector128(shifted0, shifted1, shifted2, shifted3, out t0, out t1, out t2, out t3);

            // Add inverse transform to 'ref' and store.
            // Load the reference(s).
            ref byte referenceRef = ref MemoryMarshal.GetReference(reference);

            // Load eight bytes/pixels per line.
            Vector128<byte> ref0 = Vector128.Create(Unsafe.As<byte, long>(ref referenceRef), 0).AsByte();
            Vector128<byte> ref1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps)), 0).AsByte();
            Vector128<byte> ref2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2)), 0).AsByte();
            Vector128<byte> ref3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3)), 0).AsByte();

            // Convert to 16b.
            ref0 = Vector128_.UnpackLow(ref0, Vector128<byte>.Zero);
            ref1 = Vector128_.UnpackLow(ref1, Vector128<byte>.Zero);
            ref2 = Vector128_.UnpackLow(ref2, Vector128<byte>.Zero);
            ref3 = Vector128_.UnpackLow(ref3, Vector128<byte>.Zero);

            // Add the inverse transform(s).
            Vector128<short> ref0InvAdded = ref0.AsInt16() + t0.AsInt16();
            Vector128<short> ref1InvAdded = ref1.AsInt16() + t1.AsInt16();
            Vector128<short> ref2InvAdded = ref2.AsInt16() + t2.AsInt16();
            Vector128<short> ref3InvAdded = ref3.AsInt16() + t3.AsInt16();

            // Unsigned saturate to 8b.
            ref0 = Vector128_.PackUnsignedSaturate(ref0InvAdded, ref0InvAdded);
            ref1 = Vector128_.PackUnsignedSaturate(ref1InvAdded, ref1InvAdded);
            ref2 = Vector128_.PackUnsignedSaturate(ref2InvAdded, ref2InvAdded);
            ref3 = Vector128_.PackUnsignedSaturate(ref3InvAdded, ref3InvAdded);

            // Store eight bytes/pixels per line.
            ref byte outputRef = ref MemoryMarshal.GetReference(dst);
            Unsafe.As<byte, Vector64<byte>>(ref outputRef) = ref0.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps)) = ref1.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 2)) = ref2.GetLower();
            Unsafe.As<byte, Vector64<byte>>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 3)) = ref3.GetLower();
        }
        else
        {
            ITransformOne(reference, input, dst, scratch);
            ITransformOne(reference[4..], input[16..], dst[4..], scratch);
        }
    }

    public static void ITransformOne(Span<byte> reference, Span<short> input, Span<byte> dst, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            // Load and concatenate the transform coefficients (we'll do two inverse
            // transforms in parallel). In the case of only one inverse transform, the
            // second half of the vectors will just contain random value we'll never
            // use nor store.
            ref short inputRef = ref MemoryMarshal.GetReference(input);
            Vector128<long> in0 = Vector128.Create(Unsafe.As<short, long>(ref inputRef), 0);
            Vector128<long> in1 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 4)), 0);
            Vector128<long> in2 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 8)), 0);
            Vector128<long> in3 = Vector128.Create(Unsafe.As<short, long>(ref Unsafe.Add(ref inputRef, 12)), 0);

            // a00 a10 a20 a30   x x x x
            // a01 a11 a21 a31   x x x x
            // a02 a12 a22 a32   x x x x
            // a03 a13 a23 a33   x x x x

            // Vertical pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            InverseTransformVerticalPassVector128(in0, in2, in1, in3, out Vector128<short> tmp0, out Vector128<short> tmp1, out Vector128<short> tmp2, out Vector128<short> tmp3);

            // Transpose the two 4x4.
            LossyUtils.Vp8Transpose_2_4x4_16bVector128(tmp0, tmp1, tmp2, tmp3, out Vector128<long> t0, out Vector128<long> t1, out Vector128<long> t2, out Vector128<long> t3);

            // Horizontal pass and subsequent transpose.
            // First pass, c and d calculations are longer because of the "trick" multiplications.
            InverseTransformHorizontalPassVector128(t0, t2, t1, t3, out Vector128<short> shifted0, out Vector128<short> shifted1, out Vector128<short> shifted2, out Vector128<short> shifted3);

            // Transpose the two 4x4.
            LossyUtils.Vp8Transpose_2_4x4_16bVector128(shifted0, shifted1, shifted2, shifted3, out t0, out t1, out t2, out t3);

            // Add inverse transform to 'ref' and store.
            // Load the reference(s).
            ref byte referenceRef = ref MemoryMarshal.GetReference(reference);

            // Load four bytes/pixels per line.
            Vector128<byte> ref0 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref referenceRef)).AsByte();
            Vector128<byte> ref1 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps))).AsByte();
            Vector128<byte> ref2 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2))).AsByte();
            Vector128<byte> ref3 = Vector128.CreateScalar(Unsafe.As<byte, int>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3))).AsByte();

            // Convert to 16b.
            ref0 = Vector128_.UnpackLow(ref0, Vector128<byte>.Zero);
            ref1 = Vector128_.UnpackLow(ref1, Vector128<byte>.Zero);
            ref2 = Vector128_.UnpackLow(ref2, Vector128<byte>.Zero);
            ref3 = Vector128_.UnpackLow(ref3, Vector128<byte>.Zero);

            // Add the inverse transform(s).
            Vector128<short> ref0InvAdded = ref0.AsInt16() + t0.AsInt16();
            Vector128<short> ref1InvAdded = ref1.AsInt16() + t1.AsInt16();
            Vector128<short> ref2InvAdded = ref2.AsInt16() + t2.AsInt16();
            Vector128<short> ref3InvAdded = ref3.AsInt16() + t3.AsInt16();

            // Unsigned saturate to 8b.
            ref0 = Vector128_.PackUnsignedSaturate(ref0InvAdded, ref0InvAdded);
            ref1 = Vector128_.PackUnsignedSaturate(ref1InvAdded, ref1InvAdded);
            ref2 = Vector128_.PackUnsignedSaturate(ref2InvAdded, ref2InvAdded);
            ref3 = Vector128_.PackUnsignedSaturate(ref3InvAdded, ref3InvAdded);

            // Unsigned saturate to 8b.
            ref byte outputRef = ref MemoryMarshal.GetReference(dst);

            // Store four bytes/pixels per line.
            int output0 = ref0.AsInt32().ToScalar();
            int output1 = ref1.AsInt32().ToScalar();
            int output2 = ref2.AsInt32().ToScalar();
            int output3 = ref3.AsInt32().ToScalar();

            Unsafe.As<byte, int>(ref outputRef) = output0;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps)) = output1;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 2)) = output2;
            Unsafe.As<byte, int>(ref Unsafe.Add(ref outputRef, WebpConstants.Bps * 3)) = output3;
        }
        else
        {
            int i;
            Span<int> tmp = scratch[..16];
            for (i = 0; i < 4; i++)
            {
                // vertical pass.
                int a = input[0] + input[8];
                int b = input[0] - input[8];
                int c = Mul(input[4], KC2) - Mul(input[12], KC1);
                int d = Mul(input[4], KC1) + Mul(input[12], KC2);
                tmp[0] = a + d;
                tmp[1] = b + c;
                tmp[2] = b - c;
                tmp[3] = a - d;
                tmp = tmp[4..];
                input = input[1..];
            }

            tmp = scratch;
            for (i = 0; i < 4; i++)
            {
                // horizontal pass.
                int dc = tmp[0] + 4;
                int a = dc + tmp[8];
                int b = dc - tmp[8];
                int c = Mul(tmp[4], KC2) - Mul(tmp[12], KC1);
                int d = Mul(tmp[4], KC1) + Mul(tmp[12], KC2);
                Store(dst, reference, 0, i, a + d);
                Store(dst, reference, 1, i, b + c);
                Store(dst, reference, 2, i, b - c);
                Store(dst, reference, 3, i, a - d);
                tmp = tmp[1..];
            }
        }
    }

    private static void InverseTransformVerticalPassVector128(Vector128<long> in0, Vector128<long> in2, Vector128<long> in1, Vector128<long> in3, out Vector128<short> tmp0, out Vector128<short> tmp1, out Vector128<short> tmp2, out Vector128<short> tmp3)
    {
        Vector128<short> a = in0.AsInt16() + in2.AsInt16();
        Vector128<short> b = in0.AsInt16() - in2.AsInt16();

        Vector128<short> k1 = Vector128.Create((short)20091).AsInt16();
        Vector128<short> k2 = Vector128.Create((short)-30068).AsInt16();

        // c = MUL(in1, K2) - MUL(in3, K1) = MUL(in1, k2) - MUL(in3, k1) + in1 - in3
        Vector128<short> c1 = Vector128_.MultiplyHigh(in1.AsInt16(), k2);
        Vector128<short> c2 = Vector128_.MultiplyHigh(in3.AsInt16(), k1);
        Vector128<short> c3 = in1.AsInt16() - in3.AsInt16();
        Vector128<short> c4 = c1 - c2;
        Vector128<short> c = c3 + c4;

        // d = MUL(in1, K1) + MUL(in3, K2) = MUL(in1, k1) + MUL(in3, k2) + in1 + in3
        Vector128<short> d1 = Vector128_.MultiplyHigh(in1.AsInt16(), k1);
        Vector128<short> d2 = Vector128_.MultiplyHigh(in3.AsInt16(), k2);
        Vector128<short> d3 = in1.AsInt16() + in3.AsInt16();
        Vector128<short> d4 = d1 + d2;
        Vector128<short> d = d3 + d4;

        // Second pass.
        tmp0 = a + d;
        tmp1 = b + c;
        tmp2 = b - c;
        tmp3 = a - d;
    }

    private static void InverseTransformHorizontalPassVector128(Vector128<long> t0, Vector128<long> t2, Vector128<long> t1, Vector128<long> t3, out Vector128<short> shifted0, out Vector128<short> shifted1, out Vector128<short> shifted2, out Vector128<short> shifted3)
    {
        Vector128<short> dc = t0.AsInt16() + Vector128.Create((short)4);
        Vector128<short> a = dc + t2.AsInt16();
        Vector128<short> b = dc - t2.AsInt16();

        Vector128<short> k1 = Vector128.Create((short)20091).AsInt16();
        Vector128<short> k2 = Vector128.Create((short)-30068).AsInt16();

        // c = MUL(T1, K2) - MUL(T3, K1) = MUL(T1, k2) - MUL(T3, k1) + T1 - T3
        Vector128<short> c1 = Vector128_.MultiplyHigh(t1.AsInt16(), k2);
        Vector128<short> c2 = Vector128_.MultiplyHigh(t3.AsInt16(), k1);
        Vector128<short> c3 = t1.AsInt16() - t3.AsInt16();
        Vector128<short> c4 = c1 - c2;
        Vector128<short> c = c3 + c4;

        // d = MUL(T1, K1) + MUL(T3, K2) = MUL(T1, k1) + MUL(T3, k2) + T1 + T3
        Vector128<short> d1 = Vector128_.MultiplyHigh(t1.AsInt16(), k1);
        Vector128<short> d2 = Vector128_.MultiplyHigh(t3.AsInt16(), k2);
        Vector128<short> d3 = t1.AsInt16() + t3.AsInt16();
        Vector128<short> d4 = d1 + d2;
        Vector128<short> d = d3 + d4;

        // Second pass.
        Vector128<short> tmp0 = a + d;
        Vector128<short> tmp1 = b + c;
        Vector128<short> tmp2 = b - c;
        Vector128<short> tmp3 = a - d;
        shifted0 = Vector128.ShiftRightArithmetic(tmp0, 3);
        shifted1 = Vector128.ShiftRightArithmetic(tmp1, 3);
        shifted2 = Vector128.ShiftRightArithmetic(tmp2, 3);
        shifted3 = Vector128.ShiftRightArithmetic(tmp3, 3);
    }

    public static void FTransform2(Span<byte> src, Span<byte> reference, Span<short> output, Span<short> output2, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte srcRef = ref MemoryMarshal.GetReference(src);
            ref byte referenceRef = ref MemoryMarshal.GetReference(reference);

            // Load src.
            Vector128<long> src0 = Vector128.Create(Unsafe.As<byte, long>(ref srcRef), 0);
            Vector128<long> src1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps)), 0);
            Vector128<long> src2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps * 2)), 0);
            Vector128<long> src3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps * 3)), 0);

            // Load ref.
            Vector128<long> ref0 = Vector128.Create(Unsafe.As<byte, long>(ref referenceRef), 0);
            Vector128<long> ref1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps)), 0);
            Vector128<long> ref2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2)), 0);
            Vector128<long> ref3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3)), 0);

            // Convert both to 16 bit.
            Vector128<byte> srcLow0 = Vector128_.UnpackLow(src0.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> srcLow1 = Vector128_.UnpackLow(src1.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> srcLow2 = Vector128_.UnpackLow(src2.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> srcLow3 = Vector128_.UnpackLow(src3.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> refLow0 = Vector128_.UnpackLow(ref0.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> refLow1 = Vector128_.UnpackLow(ref1.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> refLow2 = Vector128_.UnpackLow(ref2.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> refLow3 = Vector128_.UnpackLow(ref3.AsByte(), Vector128<byte>.Zero);

            // Compute difference. -> 00 01 02 03  00' 01' 02' 03'
            Vector128<short> diff0 = srcLow0.AsInt16() - refLow0.AsInt16();
            Vector128<short> diff1 = srcLow1.AsInt16() - refLow1.AsInt16();
            Vector128<short> diff2 = srcLow2.AsInt16() - refLow2.AsInt16();
            Vector128<short> diff3 = srcLow3.AsInt16() - refLow3.AsInt16();

            // Unpack and shuffle.
            // 00 01 02 03   0 0 0 0
            // 10 11 12 13   0 0 0 0
            // 20 21 22 23   0 0 0 0
            // 30 31 32 33   0 0 0 0
            Vector128<int> shuf01l = Vector128_.UnpackLow(diff0.AsInt32(), diff1.AsInt32());
            Vector128<int> shuf23l = Vector128_.UnpackLow(diff2.AsInt32(), diff3.AsInt32());
            Vector128<int> shuf01h = Vector128_.UnpackHigh(diff0.AsInt32(), diff1.AsInt32());
            Vector128<int> shuf23h = Vector128_.UnpackHigh(diff2.AsInt32(), diff3.AsInt32());

            // First pass.
            FTransformPass1Vector128(shuf01l.AsInt16(), shuf23l.AsInt16(), out Vector128<int> v01l, out Vector128<int> v32l);
            FTransformPass1Vector128(shuf01h.AsInt16(), shuf23h.AsInt16(), out Vector128<int> v01h, out Vector128<int> v32h);

            // Second pass.
            FTransformPass2Vector128(v01l, v32l, output);
            FTransformPass2Vector128(v01h, v32h, output2);
        }
        else
        {
            FTransform(src, reference, output, scratch);
            FTransform(src[4..], reference[4..], output2, scratch);
        }
    }

    public static void FTransform(Span<byte> src, Span<byte> reference, Span<short> output, Span<int> scratch)
    {
        if (Vector128.IsHardwareAccelerated)
        {
            ref byte srcRef = ref MemoryMarshal.GetReference(src);
            ref byte referenceRef = ref MemoryMarshal.GetReference(reference);

            // Load src.
            Vector128<long> src0 = Vector128.Create(Unsafe.As<byte, long>(ref srcRef), 0);
            Vector128<long> src1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps)), 0);
            Vector128<long> src2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps * 2)), 0);
            Vector128<long> src3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref srcRef, WebpConstants.Bps * 3)), 0);

            // Load ref.
            Vector128<long> ref0 = Vector128.Create(Unsafe.As<byte, long>(ref referenceRef), 0);
            Vector128<long> ref1 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps)), 0);
            Vector128<long> ref2 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 2)), 0);
            Vector128<long> ref3 = Vector128.Create(Unsafe.As<byte, long>(ref Unsafe.Add(ref referenceRef, WebpConstants.Bps * 3)), 0);

            // 00 01 02 03 *
            // 10 11 12 13 *
            // 20 21 22 23 *
            // 30 31 32 33 *
            // Shuffle.
            Vector128<short> srcLow0 = Vector128_.UnpackLow(src0.AsInt16(), src1.AsInt16());
            Vector128<short> srcLow1 = Vector128_.UnpackLow(src2.AsInt16(), src3.AsInt16());
            Vector128<short> refLow0 = Vector128_.UnpackLow(ref0.AsInt16(), ref1.AsInt16());
            Vector128<short> refLow1 = Vector128_.UnpackLow(ref2.AsInt16(), ref3.AsInt16());

            // 00 01 10 11 02 03 12 13 * * ...
            // 20 21 30 31 22 22 32 33 * * ...

            // Convert both to 16 bit.
            Vector128<byte> src0_16b = Vector128_.UnpackLow(srcLow0.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> src1_16b = Vector128_.UnpackLow(srcLow1.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> ref0_16b = Vector128_.UnpackLow(refLow0.AsByte(), Vector128<byte>.Zero);
            Vector128<byte> ref1_16b = Vector128_.UnpackLow(refLow1.AsByte(), Vector128<byte>.Zero);

            // Compute the difference.
            Vector128<short> row01 = src0_16b.AsInt16() - ref0_16b.AsInt16();
            Vector128<short> row23 = src1_16b.AsInt16() - ref1_16b.AsInt16();

            // First pass.
            FTransformPass1Vector128(row01, row23, out Vector128<int> v01, out Vector128<int> v32);

            // Second pass.
            FTransformPass2Vector128(v01, v32, output);
        }
        else
        {
            int i;
            Span<int> tmp = scratch[..16];

            int srcIdx = 0;
            int refIdx = 0;
            for (i = 0; i < 4; i++)
            {
                int d3 = src[srcIdx + 3] - reference[refIdx + 3];
                int d2 = src[srcIdx + 2] - reference[refIdx + 2];
                int d1 = src[srcIdx + 1] - reference[refIdx + 1];
                int d0 = src[srcIdx] - reference[refIdx]; // 9bit dynamic range ([-255,255])
                int a0 = d0 + d3; // 10b                      [-510,510]
                int a1 = d1 + d2;
                int a2 = d1 - d2;
                int a3 = d0 - d3;
                tmp[3 + (i * 4)] = ((a3 * 2217) - (a2 * 5352) + 937) >> 9;
                tmp[2 + (i * 4)] = (a0 - a1) * 8;
                tmp[1 + (i * 4)] = ((a2 * 2217) + (a3 * 5352) + 1812) >> 9; // [-7536,7542]
                tmp[0 + (i * 4)] = (a0 + a1) * 8; // 14b                      [-8160,8160]

                srcIdx += WebpConstants.Bps;
                refIdx += WebpConstants.Bps;
            }

            for (i = 0; i < 4; i++)
            {
                int t12 = tmp[12 + i]; // 15b
                int t8 = tmp[8 + i];

                int a1 = tmp[4 + i] + t8;
                int a2 = tmp[4 + i] - t8;
                int a0 = tmp[0 + i] + t12; // 15b
                int a3 = tmp[0 + i] - t12;

                output[12 + i] = (short)(((a3 * 2217) - (a2 * 5352) + 51000) >> 16);
                output[8 + i] = (short)((a0 - a1 + 7) >> 4);
                output[4 + i] = (short)((((a2 * 2217) + (a3 * 5352) + 12000) >> 16) + (a3 != 0 ? 1 : 0));
                output[0 + i] = (short)((a0 + a1 + 7) >> 4); // 12b
            }
        }
    }

    public static void FTransformPass1Vector128(Vector128<short> row01, Vector128<short> row23, out Vector128<int> out01, out Vector128<int> out32)
    {
        // *in01 = 00 01 10 11 02 03 12 13
        // *in23 = 20 21 30 31 22 23 32 33
        Vector128<short> shuf01_p = Vector128_.ShuffleHigh(row01, SimdUtils.Shuffle.MMShuffle2301);
        Vector128<short> shuf32_p = Vector128_.ShuffleHigh(row23, SimdUtils.Shuffle.MMShuffle2301);

        // 00 01 10 11 03 02 13 12
        // 20 21 30 31 23 22 33 32
        Vector128<long> s01 = Vector128_.UnpackLow(shuf01_p.AsInt64(), shuf32_p.AsInt64());
        Vector128<long> s32 = Vector128_.UnpackHigh(shuf01_p.AsInt64(), shuf32_p.AsInt64());

        // 00 01 10 11 20 21 30 31
        // 03 02 13 12 23 22 33 32
        Vector128<short> a01 = s01.AsInt16() + s32.AsInt16();
        Vector128<short> a32 = s01.AsInt16() - s32.AsInt16();

        // [d0 + d3 | d1 + d2 | ...] = [a0 a1 | a0' a1' | ... ]
        // [d0 - d3 | d1 - d2 | ...] = [a3 a2 | a3' a2' | ... ]

        // [ (a0 + a1) << 3, ... ]
        Vector128<int> tmp0 = Vector128_.MultiplyAddAdjacent(a01, Vector128.Create(8, 0, 8, 0, 8, 0, 8, 0, 8, 0, 8, 0, 8, 0, 8, 0).AsInt16()); // K88p

        // [ (a0 - a1) << 3, ... ]
        Vector128<int> tmp2 = Vector128_.MultiplyAddAdjacent(a01, Vector128.Create(8, 0, 248, 255, 8, 0, 248, 255, 8, 0, 248, 255, 8, 0, 248, 255).AsInt16());        // K88m
        Vector128<int> tmp11 = Vector128_.MultiplyAddAdjacent(a32, Vector128.Create(232, 20, 169, 8, 232, 20, 169, 8, 232, 20, 169, 8, 232, 20, 169, 8).AsInt16());   // K5352_2217p
        Vector128<int> tmp31 = Vector128_.MultiplyAddAdjacent(a32, Vector128.Create(169, 8, 24, 235, 169, 8, 24, 235, 169, 8, 24, 235, 169, 8, 24, 235).AsInt16());   // K5352_2217m
        Vector128<int> tmp12 = tmp11 + Vector128.Create(1812);
        Vector128<int> tmp32 = tmp31 + Vector128.Create(937);
        Vector128<int> tmp1 = Vector128.ShiftRightArithmetic(tmp12, 9);
        Vector128<int> tmp3 = Vector128.ShiftRightArithmetic(tmp32, 9);
        Vector128<short> s03 = Vector128_.PackSignedSaturate(tmp0, tmp2);
        Vector128<short> s12 = Vector128_.PackSignedSaturate(tmp1, tmp3);
        Vector128<short> slo = Vector128_.UnpackLow(s03, s12); // 0 1 0 1 0 1...
        Vector128<short> shi = Vector128_.UnpackHigh(s03, s12); // 2 3 2 3 2 3
        Vector128<int> v23 = Vector128_.UnpackHigh(slo.AsInt32(), shi.AsInt32());
        out01 = Vector128_.UnpackLow(slo.AsInt32(), shi.AsInt32());
        out32 = Vector128_.ShuffleNative(v23, SimdUtils.Shuffle.MMShuffle1032);
    }

    public static void FTransformPass2Vector128(Vector128<int> v01, Vector128<int> v32, Span<short> output)
    {
        // Same operations are done on the (0,3) and (1,2) pairs.
        // a3 = v0 - v3
        // a2 = v1 - v2
        Vector128<short> a32 = v01.AsInt16() - v32.AsInt16();
        Vector128<long> a22 = Vector128_.UnpackHigh(a32.AsInt64(), a32.AsInt64());

        Vector128<short> b23 = Vector128_.UnpackLow(a22.AsInt16(), a32.AsInt16());
        Vector128<int> c1 = Vector128_.MultiplyAddAdjacent(b23, Vector128.Create(169, 8, 232, 20, 169, 8, 232, 20, 169, 8, 232, 20, 169, 8, 232, 20).AsInt16());  // K5352_2217
        Vector128<int> c3 = Vector128_.MultiplyAddAdjacent(b23, Vector128.Create(24, 235, 169, 8, 24, 235, 169, 8, 24, 235, 169, 8, 24, 235, 169, 8).AsInt16());  // K2217_5352
        Vector128<int> d1 = c1 + Vector128.Create(12000 + (1 << 16));  // K12000PlusOne
        Vector128<int> d3 = c3 + Vector128.Create(51000);
        Vector128<int> e1 = Vector128.ShiftRightArithmetic(d1, 16);
        Vector128<int> e3 = Vector128.ShiftRightArithmetic(d3, 16);

        // f1 = ((b3 * 5352 + b2 * 2217 + 12000) >> 16)
        // f3 = ((b3 * 2217 - b2 * 5352 + 51000) >> 16)
        Vector128<short> f1 = Vector128_.PackSignedSaturate(e1, e1);
        Vector128<short> f3 = Vector128_.PackSignedSaturate(e3, e3);

        // g1 = f1 + (a3 != 0);
        // The compare will return (0xffff, 0) for (==0, !=0). To turn that into the
        // desired (0, 1), we add one earlier through k12000_plus_one.
        // -> g1 = f1 + 1 - (a3 == 0)
        Vector128<short> g1 = f1 + Vector128.Equals(a32, Vector128<short>.Zero);

        // a0 = v0 + v3
        // a1 = v1 + v2
        Vector128<short> a01 = v01.AsInt16() + v32.AsInt16();
        Vector128<short> a01Plus7 = a01.AsInt16() + Vector128.Create((short)7);
        Vector128<short> a11 = Vector128_.UnpackHigh(a01.AsInt64(), a01.AsInt64()).AsInt16();
        Vector128<short> c0 = a01Plus7 + a11;
        Vector128<short> c2 = a01Plus7 - a11;

        // d0 = (a0 + a1 + 7) >> 4;
        // d2 = (a0 - a1 + 7) >> 4;
        Vector128<short> d0 = Vector128.ShiftRightArithmetic(c0, 4);
        Vector128<short> d2 = Vector128.ShiftRightArithmetic(c2, 4);

        Vector128<long> d0g1 = Vector128_.UnpackLow(d0.AsInt64(), g1.AsInt64());
        Vector128<long> d2f3 = Vector128_.UnpackLow(d2.AsInt64(), f3.AsInt64());

        ref short outputRef = ref MemoryMarshal.GetReference(output);
        Unsafe.As<short, Vector128<short>>(ref outputRef) = d0g1.AsInt16();
        Unsafe.As<short, Vector128<short>>(ref Unsafe.Add(ref outputRef, 8)) = d2f3.AsInt16();
    }

    public static void FTransformWht(Span<short> input, Span<short> output, Span<int> scratch)
    {
        Span<int> tmp = scratch[..16];

        int i;
        int inputIdx = 0;
        for (i = 0; i < 4; i++)
        {
            int a1 = input[inputIdx + (1 * 16)] + input[inputIdx + (3 * 16)];
            int a2 = input[inputIdx + (1 * 16)] - input[inputIdx + (3 * 16)];
            int a0 = input[inputIdx + (0 * 16)] + input[inputIdx + (2 * 16)];  // 13b
            int a3 = input[inputIdx + (0 * 16)] - input[inputIdx + (2 * 16)];
            tmp[3 + (i * 4)] = a0 - a1;
            tmp[2 + (i * 4)] = a3 - a2;
            tmp[1 + (i * 4)] = a3 + a2;
            tmp[0 + (i * 4)] = a0 + a1;   // 14b

            inputIdx += 64;
        }

        for (i = 0; i < 4; i++)
        {
            int t12 = tmp[12 + i];
            int t8 = tmp[8 + i];

            int a1 = tmp[4 + i] + t12;
            int a2 = tmp[4 + i] - t12;
            int a0 = tmp[0 + i] + t8;  // 15b
            int a3 = tmp[0 + i] - t8;

            int b0 = a0 + a1;    // 16b
            int b1 = a3 + a2;
            int b2 = a3 - a2;
            int b3 = a0 - a1;

            output[12 + i] = (short)(b3 >> 1);
            output[8 + i] = (short)(b2 >> 1);
            output[4 + i] = (short)(b1 >> 1);
            output[0 + i] = (short)(b0 >> 1);     // 15b
        }
    }

    // luma 16x16 prediction (paragraph 12.3).
    public static void EncPredLuma16(Span<byte> dst, Span<byte> left, Span<byte> top)
    {
        DcMode(dst, left, top, 16, 16, 5);
        VerticalPred(dst[I16VE16..], top, 16);
        HorizontalPred(dst[I16HE16..], left, 16);
        TrueMotion(dst[I16TM16..], left, top, 16);
    }

    // Chroma 8x8 prediction (paragraph 12.2).
    public static void EncPredChroma8(Span<byte> dst, Span<byte> left, Span<byte> top)
    {
        // U block.
        DcMode(dst[C8DC8..], left, top, 8, 8, 4);
        VerticalPred(dst[C8VE8..], top, 8);
        HorizontalPred(dst[C8HE8..], left, 8);
        TrueMotion(dst[C8TM8..], left, top, 8);

        // V block.
        dst = dst[8..];
        if (!top.IsEmpty)
        {
            top = top[8..];
        }

        if (!left.IsEmpty)
        {
            left = left[16..];
        }

        DcMode(dst[C8DC8..], left, top, 8, 8, 4);
        VerticalPred(dst[C8VE8..], top, 8);
        HorizontalPred(dst[C8HE8..], left, 8);
        TrueMotion(dst[C8TM8..], left, top, 8);
    }

    // Left samples are top[-5 .. -2], top_left is top[-1], top are
    // located at top[0..3], and top right is top[4..7]
    public static void EncPredLuma4(Span<byte> dst, Span<byte> top, int topOffset, Span<byte> vals)
    {
        Dc4(dst[I4DC4..], top, topOffset);
        Tm4(dst[I4TM4..], top, topOffset);
        Ve4(dst[I4VE4..], top, topOffset, vals);
        He4(dst[I4HE4..], top, topOffset);
        Rd4(dst[I4RD4..], top, topOffset);
        Vr4(dst[I4VR4..], top, topOffset);
        Ld4(dst[I4LD4..], top, topOffset);
        Vl4(dst[I4VL4..], top, topOffset);
        Hd4(dst[I4HD4..], top, topOffset);
        Hu4(dst[I4HU4..], top, topOffset);
    }

    private static void VerticalPred(Span<byte> dst, Span<byte> top, int size)
    {
        if (!top.IsEmpty)
        {
            for (int j = 0; j < size; j++)
            {
                top[..size].CopyTo(dst[(j * WebpConstants.Bps)..]);
            }
        }
        else
        {
            Fill(dst, 127, size);
        }
    }

    public static void HorizontalPred(Span<byte> dst, Span<byte> left, int size)
    {
        if (!left.IsEmpty)
        {
            left = left[1..]; // in the reference implementation, left starts at - 1.
            for (int j = 0; j < size; j++)
            {
                dst.Slice(j * WebpConstants.Bps, size).Fill(left[j]);
            }
        }
        else
        {
            Fill(dst, 129, size);
        }
    }

    public static void TrueMotion(Span<byte> dst, Span<byte> left, Span<byte> top, int size)
    {
        if (!left.IsEmpty)
        {
            if (!top.IsEmpty)
            {
                Span<byte> clip = Clip1.AsSpan(255 - left[0]); // left [0] instead of left[-1], original left starts at -1
                for (int y = 0; y < size; y++)
                {
                    Span<byte> clipTable = clip[left[y + 1]..]; // left[y]
                    for (int x = 0; x < size; x++)
                    {
                        dst[x] = clipTable[top[x]];
                    }

                    dst = dst[WebpConstants.Bps..];
                }
            }
            else
            {
                HorizontalPred(dst, left, size);
            }
        }
        else
        {
            // true motion without left samples (hence: with default 129 value)
            // is equivalent to VE prediction where you just copy the top samples.
            // Note that if top samples are not available, the default value is
            // then 129, and not 127 as in the VerticalPred case.
            if (!top.IsEmpty)
            {
                VerticalPred(dst, top, size);
            }
            else
            {
                Fill(dst, 129, size);
            }
        }
    }

    private static void DcMode(Span<byte> dst, Span<byte> left, Span<byte> top, int size, int round, int shift)
    {
        int dc = 0;
        int j;
        if (!top.IsEmpty)
        {
            for (j = 0; j < size; j++)
            {
                dc += top[j];
            }

            if (!left.IsEmpty)
            {
                // top and left present.
                left = left[1..]; // in the reference implementation, left starts at -1.
                for (j = 0; j < size; j++)
                {
                    dc += left[j];
                }
            }
            else
            {
                // top, but no left.
                dc += dc;
            }

            dc = (dc + round) >> shift;
        }
        else if (!left.IsEmpty)
        {
            // left but no top.
            left = left[1..]; // in the reference implementation, left starts at -1.
            for (j = 0; j < size; j++)
            {
                dc += left[j];
            }

            dc += dc;
            dc = (dc + round) >> shift;
        }
        else
        {
            // no top, no left, nothing.
            dc = 0x80;
        }

        Fill(dst, dc, size);
    }

    private static void Dc4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        uint dc = 4;
        int i;
        for (i = 0; i < 4; i++)
        {
            dc += (uint)(top[topOffset + i] + top[topOffset - 5 + i]);
        }

        Fill(dst, (int)(dc >> 3), 4);
    }

    private static void Tm4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        Span<byte> clip = Clip1.AsSpan(255 - top[topOffset - 1]);
        for (int y = 0; y < 4; y++)
        {
            Span<byte> clipTable = clip[top[topOffset - 2 - y]..];
            for (int x = 0; x < 4; x++)
            {
                dst[x] = clipTable[top[topOffset + x]];
            }

            dst = dst[WebpConstants.Bps..];
        }
    }

    private static void Ve4(Span<byte> dst, Span<byte> top, int topOffset, Span<byte> vals)
    {
        // vertical
        vals[0] = LossyUtils.Avg3(top[topOffset - 1], top[topOffset], top[topOffset + 1]);
        vals[1] = LossyUtils.Avg3(top[topOffset], top[topOffset + 1], top[topOffset + 2]);
        vals[2] = LossyUtils.Avg3(top[topOffset + 1], top[topOffset + 2], top[topOffset + 3]);
        vals[3] = LossyUtils.Avg3(top[topOffset + 2], top[topOffset + 3], top[topOffset + 4]);
        for (int i = 0; i < 4; i++)
        {
            vals.CopyTo(dst[(i * WebpConstants.Bps)..]);
        }
    }

    private static void He4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        // horizontal
        byte x = top[topOffset - 1];
        byte i = top[topOffset - 2];
        byte j = top[topOffset - 3];
        byte k = top[topOffset - 4];
        byte l = top[topOffset - 5];

        uint val = 0x01010101U * LossyUtils.Avg3(x, i, j);
        BinaryPrimitives.WriteUInt32BigEndian(dst, val);
        val = 0x01010101U * LossyUtils.Avg3(i, j, k);
        BinaryPrimitives.WriteUInt32BigEndian(dst[(1 * WebpConstants.Bps)..], val);
        val = 0x01010101U * LossyUtils.Avg3(j, k, l);
        BinaryPrimitives.WriteUInt32BigEndian(dst[(2 * WebpConstants.Bps)..], val);
        val = 0x01010101U * LossyUtils.Avg3(k, l, l);
        BinaryPrimitives.WriteUInt32BigEndian(dst[(3 * WebpConstants.Bps)..], val);
    }

    private static void Rd4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte x = top[topOffset - 1];
        byte i = top[topOffset - 2];
        byte j = top[topOffset - 3];
        byte k = top[topOffset - 4];
        byte l = top[topOffset - 5];
        byte a = top[topOffset];
        byte b = top[topOffset + 1];
        byte c = top[topOffset + 2];
        byte d = top[topOffset + 3];

        LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(j, k, l));
        byte ijk = LossyUtils.Avg3(i, j, k);
        LossyUtils.Dst(dst, 0, 2, ijk);
        LossyUtils.Dst(dst, 1, 3, ijk);
        byte xij = LossyUtils.Avg3(x, i, j);
        LossyUtils.Dst(dst, 0, 1, xij);
        LossyUtils.Dst(dst, 1, 2, xij);
        LossyUtils.Dst(dst, 2, 3, xij);
        byte axi = LossyUtils.Avg3(a, x, i);
        LossyUtils.Dst(dst, 0, 0, axi);
        LossyUtils.Dst(dst, 1, 1, axi);
        LossyUtils.Dst(dst, 2, 2, axi);
        LossyUtils.Dst(dst, 3, 3, axi);
        byte bax = LossyUtils.Avg3(b, a, x);
        LossyUtils.Dst(dst, 1, 0, bax);
        LossyUtils.Dst(dst, 2, 1, bax);
        LossyUtils.Dst(dst, 3, 2, bax);
        byte cba = LossyUtils.Avg3(c, b, a);
        LossyUtils.Dst(dst, 2, 0, cba);
        LossyUtils.Dst(dst, 3, 1, cba);
        LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(d, c, b));
    }

    private static void Vr4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte x = top[topOffset - 1];
        byte i = top[topOffset - 2];
        byte j = top[topOffset - 3];
        byte k = top[topOffset - 4];
        byte a = top[topOffset];
        byte b = top[topOffset + 1];
        byte c = top[topOffset + 2];
        byte d = top[topOffset + 3];

        byte xa = LossyUtils.Avg2(x, a);
        LossyUtils.Dst(dst, 0, 0, xa);
        LossyUtils.Dst(dst, 1, 2, xa);
        byte ab = LossyUtils.Avg2(a, b);
        LossyUtils.Dst(dst, 1, 0, ab);
        LossyUtils.Dst(dst, 2, 2, ab);
        byte bc = LossyUtils.Avg2(b, c);
        LossyUtils.Dst(dst, 2, 0, bc);
        LossyUtils.Dst(dst, 3, 2, bc);
        LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg2(c, d));
        LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg3(k, j, i));
        LossyUtils.Dst(dst, 0, 2, LossyUtils.Avg3(j, i, x));
        byte ixa = LossyUtils.Avg3(i, x, a);
        LossyUtils.Dst(dst, 0, 1, ixa);
        LossyUtils.Dst(dst, 1, 3, ixa);
        byte xab = LossyUtils.Avg3(x, a, b);
        LossyUtils.Dst(dst, 1, 1, xab);
        LossyUtils.Dst(dst, 2, 3, xab);
        byte abc = LossyUtils.Avg3(a, b, c);
        LossyUtils.Dst(dst, 2, 1, abc);
        LossyUtils.Dst(dst, 3, 3, abc);
        LossyUtils.Dst(dst, 3, 1, LossyUtils.Avg3(b, c, d));
    }

    private static void Ld4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte a = top[topOffset + 0];
        byte b = top[topOffset + 1];
        byte c = top[topOffset + 2];
        byte d = top[topOffset + 3];
        byte e = top[topOffset + 4];
        byte f = top[topOffset + 5];
        byte g = top[topOffset + 6];
        byte h = top[topOffset + 7];

        LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg3(a, b, c));
        byte bcd = LossyUtils.Avg3(b, c, d);
        LossyUtils.Dst(dst, 1, 0, bcd);
        LossyUtils.Dst(dst, 0, 1, bcd);
        byte cde = LossyUtils.Avg3(c, d, e);
        LossyUtils.Dst(dst, 2, 0, cde);
        LossyUtils.Dst(dst, 1, 1, cde);
        LossyUtils.Dst(dst, 0, 2, cde);
        byte def = LossyUtils.Avg3(d, e, f);
        LossyUtils.Dst(dst, 3, 0, def);
        LossyUtils.Dst(dst, 2, 1, def);
        LossyUtils.Dst(dst, 1, 2, def);
        LossyUtils.Dst(dst, 0, 3, def);
        byte efg = LossyUtils.Avg3(e, f, g);
        LossyUtils.Dst(dst, 3, 1, efg);
        LossyUtils.Dst(dst, 2, 2, efg);
        LossyUtils.Dst(dst, 1, 3, efg);
        byte fgh = LossyUtils.Avg3(f, g, h);
        LossyUtils.Dst(dst, 3, 2, fgh);
        LossyUtils.Dst(dst, 2, 3, fgh);
        LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(g, h, h));
    }

    private static void Vl4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte a = top[topOffset + 0];
        byte b = top[topOffset + 1];
        byte c = top[topOffset + 2];
        byte d = top[topOffset + 3];
        byte e = top[topOffset + 4];
        byte f = top[topOffset + 5];
        byte g = top[topOffset + 6];
        byte h = top[topOffset + 7];

        LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(a, b));
        byte bc = LossyUtils.Avg2(b, c);
        LossyUtils.Dst(dst, 1, 0, bc);
        LossyUtils.Dst(dst, 0, 2, bc);
        byte cd = LossyUtils.Avg2(c, d);
        LossyUtils.Dst(dst, 2, 0, cd);
        LossyUtils.Dst(dst, 1, 2, cd);
        byte de = LossyUtils.Avg2(d, e);
        LossyUtils.Dst(dst, 3, 0, de);
        LossyUtils.Dst(dst, 2, 2, de);
        LossyUtils.Dst(dst, 0, 1, LossyUtils.Avg3(a, b, c));
        byte bcd = LossyUtils.Avg3(b, c, d);
        LossyUtils.Dst(dst, 1, 1, bcd);
        LossyUtils.Dst(dst, 0, 3, bcd);
        byte cde = LossyUtils.Avg3(c, d, e);
        LossyUtils.Dst(dst, 2, 1, cde);
        LossyUtils.Dst(dst, 1, 3, cde);
        byte def = LossyUtils.Avg3(d, e, f);
        LossyUtils.Dst(dst, 3, 1, def);
        LossyUtils.Dst(dst, 2, 3, def);
        LossyUtils.Dst(dst, 3, 2, LossyUtils.Avg3(e, f, g));
        LossyUtils.Dst(dst, 3, 3, LossyUtils.Avg3(f, g, h));
    }

    private static void Hd4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte x = top[topOffset - 1];
        byte i = top[topOffset - 2];
        byte j = top[topOffset - 3];
        byte k = top[topOffset - 4];
        byte l = top[topOffset - 5];
        byte a = top[topOffset];
        byte b = top[topOffset + 1];
        byte c = top[topOffset + 2];

        byte ix = LossyUtils.Avg2(i, x);
        LossyUtils.Dst(dst, 0, 0, ix);
        LossyUtils.Dst(dst, 2, 1, ix);
        byte ji = LossyUtils.Avg2(j, i);
        LossyUtils.Dst(dst, 0, 1, ji);
        LossyUtils.Dst(dst, 2, 2, ji);
        byte kj = LossyUtils.Avg2(k, j);
        LossyUtils.Dst(dst, 0, 2, kj);
        LossyUtils.Dst(dst, 2, 3, kj);
        LossyUtils.Dst(dst, 0, 3, LossyUtils.Avg2(l, k));
        LossyUtils.Dst(dst, 3, 0, LossyUtils.Avg3(a, b, c));
        LossyUtils.Dst(dst, 2, 0, LossyUtils.Avg3(x, a, b));
        byte ixa = LossyUtils.Avg3(i, x, a);
        LossyUtils.Dst(dst, 1, 0, ixa);
        LossyUtils.Dst(dst, 3, 1, ixa);
        byte jix = LossyUtils.Avg3(j, i, x);
        LossyUtils.Dst(dst, 1, 1, jix);
        LossyUtils.Dst(dst, 3, 2, jix);
        byte kji = LossyUtils.Avg3(k, j, i);
        LossyUtils.Dst(dst, 1, 2, kji);
        LossyUtils.Dst(dst, 3, 3, kji);
        LossyUtils.Dst(dst, 1, 3, LossyUtils.Avg3(l, k, j));
    }

    private static void Hu4(Span<byte> dst, Span<byte> top, int topOffset)
    {
        byte i = top[topOffset - 2];
        byte j = top[topOffset - 3];
        byte k = top[topOffset - 4];
        byte l = top[topOffset - 5];

        LossyUtils.Dst(dst, 0, 0, LossyUtils.Avg2(i, j));
        byte jk = LossyUtils.Avg2(j, k);
        LossyUtils.Dst(dst, 2, 0, jk);
        LossyUtils.Dst(dst, 0, 1, jk);
        byte kl = LossyUtils.Avg2(k, l);
        LossyUtils.Dst(dst, 2, 1, kl);
        LossyUtils.Dst(dst, 0, 2, kl);
        LossyUtils.Dst(dst, 1, 0, LossyUtils.Avg3(i, j, k));
        byte jkl = LossyUtils.Avg3(j, k, l);
        LossyUtils.Dst(dst, 3, 0, jkl);
        LossyUtils.Dst(dst, 1, 1, jkl);
        byte kll = LossyUtils.Avg3(k, l, l);
        LossyUtils.Dst(dst, 3, 1, kll);
        LossyUtils.Dst(dst, 1, 2, kll);
        LossyUtils.Dst(dst, 3, 2, l);
        LossyUtils.Dst(dst, 2, 2, l);
        LossyUtils.Dst(dst, 0, 3, l);
        LossyUtils.Dst(dst, 1, 3, l);
        LossyUtils.Dst(dst, 2, 3, l);
        LossyUtils.Dst(dst, 3, 3, l);
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void Fill(Span<byte> dst, int value, int size)
    {
        for (int j = 0; j < size; j++)
        {
            dst.Slice(j * WebpConstants.Bps, size).Fill((byte)value);
        }
    }

    [MethodImpl(InliningOptions.ShortMethod)]
    private static byte Clip8b(int v) => (v & ~0xff) == 0 ? (byte)v : v < 0 ? (byte)0 : (byte)255;

    [MethodImpl(InliningOptions.ShortMethod)]
    private static void Store(Span<byte> dst, Span<byte> reference, int x, int y, int v) => dst[x + (y * WebpConstants.Bps)] = LossyUtils.Clip8B(reference[x + (y * WebpConstants.Bps)] + (v >> 3));

    [MethodImpl(InliningOptions.ShortMethod)]
    private static int Mul(int a, int b) => (a * b) >> 16;
}
