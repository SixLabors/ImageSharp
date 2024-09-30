// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct4ForwardTransformer : IAv1ForwardTransformer
{
    public void Transform(ref int input, ref int output, int cosBit, Span<byte> stageRange)
    {
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        ref int bf0 = ref output;
        ref int bf1 = ref output;
        Span<int> stepSpan = new int[4];
        ref int step0 = ref stepSpan[0];
        ref int step1 = ref Unsafe.Add(ref step0, 1);
        ref int step2 = ref Unsafe.Add(ref step0, 2);
        ref int step3 = ref Unsafe.Add(ref step0, 3);
        ref int output1 = ref Unsafe.Add(ref output, 1);
        ref int output2 = ref Unsafe.Add(ref output, 2);
        ref int output3 = ref Unsafe.Add(ref output, 3);

        // stage 0;

        // stage 1;
        output = input + Unsafe.Add(ref input, 3);
        output1 = Unsafe.Add(ref input, 1) + Unsafe.Add(ref input, 2);
        output2 = -Unsafe.Add(ref input, 2) + Unsafe.Add(ref input, 1);
        output3 = -Unsafe.Add(ref input, 3) + Unsafe.Add(ref input, 0);

        // stage 2
        step0 = HalfBtf(cospi[32], output, cospi[32], output1, cosBit);
        step1 = HalfBtf(-cospi[32], output1, cospi[32], output, cosBit);
        step2 = HalfBtf(cospi[48], output2, cospi[16], output3, cosBit);
        step3 = HalfBtf(cospi[48], output3, -cospi[16], output2, cosBit);

        // stage 3
        output = step0;
        output1 = step2;
        output2 = step1;
        output3 = step3;
    }

    public void TransformAvx2(ref Vector256<int> input, ref Vector256<int> output, int cosBit, int columnNumber)
        => throw new NotImplementedException("Too small block for Vector implementation, use TransformSse() method instead.");

    /// <summary>
    /// SVT: fdct4x4_sse4_1
    /// </summary>
    public static void TransformSse(ref Vector128<int> input, ref Vector128<int> output, byte cosBit, int columnNumber)
    {
#pragma warning disable CA1857 // A constant is expected for the parameter

        // We only use stage-2 bit;
        // shift[0] is used in load_buffer_4x4()
        // shift[1] is used in txfm_func_col()
        // shift[2] is used in txfm_func_row()
        Span<int> cospi = Av1SinusConstants.CosinusPi(cosBit);
        Vector128<int> cospi32 = Vector128.Create<int>(cospi[32]);
        Vector128<int> cospi48 = Vector128.Create<int>(cospi[48]);
        Vector128<int> cospi16 = Vector128.Create<int>(cospi[16]);
        Vector128<int> rnding = Vector128.Create<int>(1 << (cosBit - 1));
        Vector128<int> s0, s1, s2, s3;
        Vector128<int> u0, u1, u2, u3;
        Vector128<int> v0, v1, v2, v3;

        int endidx = 3 * columnNumber;
        s0 = Sse2.Add(input, Unsafe.Add(ref input, endidx));
        s3 = Sse2.Subtract(input, Unsafe.Add(ref input, endidx));
        endidx -= columnNumber;
        s1 = Sse2.Add(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));
        s2 = Sse2.Subtract(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));

        // btf_32_sse4_1_type0(cospi32, cospi32, s[01], u[02], bit);
        u0 = Sse41.MultiplyLow(s0, cospi32);
        u1 = Sse41.MultiplyLow(s1, cospi32);
        u2 = Sse2.Add(u0, u1);
        v0 = Sse2.Subtract(u0, u1);

        u3 = Sse2.Add(u2, rnding);
        v1 = Sse2.Add(v0, rnding);

        u0 = Sse2.ShiftRightArithmetic(u3, cosBit);
        u2 = Sse2.ShiftRightArithmetic(v1, cosBit);

        // btf_32_sse4_1_type1(cospi48, cospi16, s[23], u[13], bit);
        v0 = Sse41.MultiplyLow(s2, cospi48);
        v1 = Sse41.MultiplyLow(s3, cospi16);
        v2 = Sse2.Add(v0, v1);

        v3 = Sse2.Add(v2, rnding);
        u1 = Sse2.ShiftRightArithmetic(v3, cosBit);

        v0 = Sse41.MultiplyLow(s2, cospi16);
        v1 = Sse41.MultiplyLow(s3, cospi48);
        v2 = Sse2.Subtract(v1, v0);

        v3 = Sse2.Add(v2, rnding);
        u3 = Sse2.ShiftRightArithmetic(v3, cosBit);

        // Note: shift[1] and shift[2] are zeros

        // Transpose 4x4 32-bit
        v0 = Sse2.UnpackLow(u0, u1);
        v1 = Sse2.UnpackHigh(u0, u1);
        v2 = Sse2.UnpackLow(u2, u3);
        v3 = Sse2.UnpackHigh(u2, u3);

        output = Sse2.UnpackLow(v0.AsInt64(), v2.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 1) = Sse2.UnpackHigh(v0.AsInt64(), v2.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 2) = Sse2.UnpackLow(v1.AsInt64(), v3.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 3) = Sse2.UnpackHigh(v1.AsInt64(), v3.AsInt64()).AsInt32();
#pragma warning restore CA1857 // A constant is expected for the parameter
    }

    private static int HalfBtf(int w0, int in0, int w1, int in1, int bit)
    {
        long result64 = (long)(w0 * in0) + (w1 * in1);
        long intermediate = result64 + (1L << (bit - 1));

        // NOTE(david.barker): The value 'result_64' may not necessarily fit
        // into 32 bits. However, the result of this function is nominally
        // ROUND_POWER_OF_TWO_64(result_64, bit)
        // and that is required to fit into stage_range[stage] many bits
        // (checked by range_check_buf()).
        //
        // Here we've unpacked that rounding operation, and it can be shown
        // that the value of 'intermediate' here *does* fit into 32 bits
        // for any conformant bitstream.
        // The upshot is that, if you do all this calculation using
        // wrapping 32-bit arithmetic instead of (non-wrapping) 64-bit arithmetic,
        // then you'll still get the correct result.
        return (int)(intermediate >> bit);
    }
}
