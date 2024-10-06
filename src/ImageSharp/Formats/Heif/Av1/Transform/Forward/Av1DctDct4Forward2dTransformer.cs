// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

internal class Av1DctDct4Forward2dTransformer : Av1Forward2dTransformerBase
{
    private readonly Av1Transform2dFlipConfiguration config = new(Av1TransformType.DctDct, Av1TransformSize.Size4x4);
    private readonly Av1Dct4Forward1dTransformer transformer = new();
    private readonly int[] temp = new int[Av1Constants.MaxTransformSize * Av1Constants.MaxTransformSize];

    public void Transform(Span<short> input, Span<int> output, int cosBit, int columnNumber)
    {
        /*if (Vector256.IsHardwareAccelerated)
        {
            Span<Vector128<int>> inputVectors = stackalloc Vector128<int>[16];
            ref Vector128<int> outputAsVector = ref Unsafe.As<int, Vector128<int>>(ref output);
            TransformVector(ref inputVectors[0], ref outputAsVector, cosBit, columnNumber);
        }
        else*/
        {
            Transform2dCore(this.transformer, this.transformer, input, 4, output, this.config, this.temp, 8);
        }
    }

    /// <summary>
    /// SVT: fdct4x4_sse4_1
    /// </summary>
    private static void TransformVector(ref Vector128<int> input, ref Vector128<int> output, int cosBit, int columnNumber)
    {
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
        Vector256<int> interleave32 = Vector256.Create(0, 4, 1, 5, 2, 6, 3, 7);
        Vector256<int> reverse64 = Vector256.Create(1, 0, 3, 2, 5, 4, 7, 6);
        Vector256<int> select64 = Vector256.Create(0, 0, -1, -1, 0, 0, -1, -1);

        int endidx = 3 * columnNumber;
        s0 = Vector128.Add(input, Unsafe.Add(ref input, endidx));
        s3 = Vector128.Subtract(input, Unsafe.Add(ref input, endidx));
        endidx -= columnNumber;
        s1 = Vector128.Add(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));
        s2 = Vector128.Subtract(Unsafe.Add(ref input, columnNumber), Unsafe.Add(ref input, endidx));

        // btf_32_sse4_1_type0(cospi32, cospi32, s[01], u[02], bit);
        u0 = Vector128.Multiply(s0, cospi32);
        u1 = Vector128.Multiply(s1, cospi32);
        u2 = Vector128.Add(u0, u1);
        v0 = Vector128.Subtract(u0, u1);

        u3 = Vector128.Add(u2, rnding);
        v1 = Vector128.Add(v0, rnding);

        u0 = Vector128.ShiftRightArithmetic(u3, cosBit);
        u2 = Vector128.ShiftRightArithmetic(v1, cosBit);

        // btf_32_sse4_1_type1(cospi48, cospi16, s[23], u[13], bit);
        v0 = Vector128.Multiply(s2, cospi48);
        v1 = Vector128.Multiply(s3, cospi16);
        v2 = Vector128.Add(v0, v1);

        v3 = Vector128.Add(v2, rnding);
        u1 = Vector128.ShiftRightArithmetic(v3, cosBit);

        v0 = Vector128.Multiply(s2, cospi16);
        v1 = Vector128.Multiply(s3, cospi48);
        v2 = Vector128.Subtract(v1, v0);

        v3 = Vector128.Add(v2, rnding);
        u3 = Vector128.ShiftRightArithmetic(v3, cosBit);

        // Note: shift[1] and shift[2] are zeros

        // Transpose 4x4 32-bit
        Vector256<int> w0 = Vector256.Create(u0, u1);
        Vector256<int> w1 = Vector256.Create(u2, u3);
        w0 = Vector256.Shuffle(w0, interleave32);
        w1 = Vector256.Shuffle(w1, interleave32);
        Vector256<int> w2 = Vector256.ConditionalSelect(select64, w0, w1);
        Vector256<int> w3 = Vector256.ConditionalSelect(select64, w1, w0);
        w3 = Vector256.Shuffle(w3, reverse64);

        output = Vector256.GetLower(w2);
        Unsafe.Add(ref output, 1) = Vector256.GetLower(w3);
        Unsafe.Add(ref output, 2) = Vector256.GetUpper(w2);
        Unsafe.Add(ref output, 3) = Vector256.GetUpper(w3);
    }
}
