// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1Dct4ForwardTransformer : IAv1ForwardTransformer
{
    public void Transform(ref int input, ref int output, int cosBit, Span<byte> stageRange)
        => throw new NotImplementedException();

    public void TransformAvx2(ref Vector256<int> input, ref Vector256<int> output, int cosBit, int columnNumber)
        => throw new NotImplementedException("Too small block for Vector implementation, use TransformSse() method instead.");

    /// <summary>
    /// SVT: fdct4x4_sse4_1
    /// </summary>
    public static void TransformSse(ref Vector128<int> input, ref Vector128<int> output, byte cosBit, int columnNumber)
    {
        /*
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
        s0 = Sse41.Add(input, Unsafe.Add(ref input, endidx));
        s3 = Sse41.Subtract(input, Unsafe.Add(ref input, endidx));
        endidx -= columnNumber;
        s1 = Sse41.Add(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));
        s2 = Sse41.Subtract(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));

        // btf_32_sse4_1_type0(cospi32, cospi32, s[01], u[02], bit);
        u0 = Sse41.MultiplyLow(s0, cospi32);
        u1 = Sse41.MultiplyLow(s1, cospi32);
        u2 = Sse41.Add(u0, u1);
        v0 = Sse41.Subtract(u0, u1);

        u3 = Sse41.Add(u2, rnding);
        v1 = Sse41.Add(v0, rnding);

        u0 = Sse41.ShiftRightArithmetic(u3, cosBit);
        u2 = Sse41.ShiftRightArithmetic(v1, cosBit);

        // btf_32_sse4_1_type1(cospi48, cospi16, s[23], u[13], bit);
        v0 = Sse41.MultiplyLow(s2, cospi48);
        v1 = Sse41.MultiplyLow(s3, cospi16);
        v2 = Sse41.Add(v0, v1);

        v3 = Sse41.Add(v2, rnding);
        u1 = Sse41.ShiftRightArithmetic(v3, cosBit);

        v0 = Sse41.MultiplyLow(s2, cospi16);
        v1 = Sse41.MultiplyLow(s3, cospi48);
        v2 = Sse41.Subtract(v1, v0);

        v3 = Sse41.Add(v2, rnding);
        u3 = Sse41.ShiftRightArithmetic(v3, cosBit);

        // Note: shift[1] and shift[2] are zeros

        // Transpose 4x4 32-bit
        v0 = Sse41.UnpackLow(u0, u1);
        v1 = Sse41.UnpackHigh(u0, u1);
        v2 = Sse41.UnpackLow(u2, u3);
        v3 = Sse41.UnpackHigh(u2, u3);

        output = Sse41.UnpackLow(v0.AsInt64(), v2.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 1) = Sse41.UnpackHigh(v0.AsInt64(), v2.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 2) = Sse41.UnpackLow(v1.AsInt64(), v3.AsInt64()).AsInt32();
        Unsafe.Add(ref output, 3) = Sse41.UnpackHigh(v1.AsInt64(), v3.AsInt64()).AsInt32();
        */
    }
}
