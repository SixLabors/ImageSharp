// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

internal static class FjxlTokenization
{
    public static void Tokenize16(Span<short> residuals, Span<short> output, Span<short> nbitsOut, Span<short> bitsOut)
    {
        Vector<short> res = Vector.Create<short>(residuals);
        Vector<short> token = FjxlSimdUtils.ValueToToken(res);
        Vector<short> nbits = FjxlSimdUtils.SaturateSubtract(token, Vector<short>.One);
        Vector<short> bits = FjxlSimdUtils.SaturateSubtract(res, FjxlSimdUtils.Pow2(nbits));

        token.CopyTo(output);
        nbits.CopyTo(nbitsOut);
        bits.CopyTo(bitsOut);
    }

    public static void Tokenize32(Span<int> residuals, Span<short> tokenOut, Span<int> nbitsOut, Span<int> bitsOut)
    {
        Vector<int> resLo = Vector.Create<int>(residuals);
        Vector<int> resHi = Vector.Create<int>(residuals[Vector<int>.Count..]);

        Vector<int> tokenLo = FjxlSimdUtils.ValueToToken(resLo);
        Vector<int> tokenHi = FjxlSimdUtils.ValueToToken(resHi);

        Vector<int> nbitsLo = FjxlSimdUtils.SaturateSubtract(tokenLo, Vector<int>.One);
        Vector<int> nbitsHi = FjxlSimdUtils.SaturateSubtract(tokenHi, Vector<int>.One);

        Vector<int> bitsLo = FjxlSimdUtils.SaturateSubtract(resLo, FjxlSimdUtils.Pow2(nbitsLo));
        Vector<int> bitsHi = FjxlSimdUtils.SaturateSubtract(resHi, FjxlSimdUtils.Pow2(nbitsHi));

        Vector<short> token = FjxlSimdUtils.FromTwo32(tokenLo, tokenHi);
        token.CopyTo(tokenOut);

        nbitsLo.CopyTo(nbitsOut);
        nbitsHi.CopyTo(nbitsOut[Vector<int>.Count..]);

        bitsLo.CopyTo(bitsOut);
        bitsHi.CopyTo(bitsOut[Vector<int>.Count..]);
    }
}
