// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

internal static class FjxlHuffman
{
    public static void HuffmanSimdUpTo13(Span<short> tokens, ReadOnlySpan<byte> rawNbitsSimd, ReadOnlySpan<byte> rawBitsSimd, Span<short> nbitsOut, Span<short> bitsOut)
    {
        Vector<short> tok = FjxlSimdUtils.PrepareForU8Lookup(Vector.Create<short>(tokens));

        FjxlSimdUtils.U8Lookup(tok, rawNbitsSimd).CopyTo(nbitsOut);
        FjxlSimdUtils.U8Lookup(tok, rawBitsSimd).CopyTo(bitsOut);
    }

    public static void HuffmanSimd14(Span<short> tokens, ReadOnlySpan<byte> rawNbitsSimd, ReadOnlySpan<byte> rawBitsSimd, Span<short> nbitsOut, Span<short> bitsOut)
    {
        Vector<short> tokenCap = Vector.Create((short)15);
        Vector<short> tok = Vector.Create<short>(tokens);

        Vector<short> tokIndex = FjxlSimdUtils.PrepareForU8Lookup(Vector.Min(tok, tokenCap));

        Vector<short> huffBitsPre = FjxlSimdUtils.U8Lookup(tokIndex, rawBitsSimd);

        Vector<short> needsHighBit = Vector.Equals(tok, Vector.Create((short)16));

        Vector<short> huffBits = Vector.ConditionalSelect(
            needsHighBit,
            huffBitsPre | Vector.Create((short)128),
            huffBitsPre);

        huffBits.CopyTo(bitsOut);

        FjxlSimdUtils.U8Lookup(tokIndex, rawNbitsSimd)
            .CopyTo(nbitsOut);
    }

    public static void HuffmanSimdAbove14(Span<short> tokens, ReadOnlySpan<byte> rawNbitsSimd, ReadOnlySpan<byte> rawBitsSimd, Span<short> nbitsOut, Span<short> bitsOut)
    {
        Vector<short> tok = Vector.Create<short>(tokens);
        Vector<short> above = Vector.GreaterThan(tok, Vector.Create((short)12));

        Vector<short> remapTok = Vector.ConditionalSelect(
            above,
            FjxlSimdUtils.HorizontalAdd(tok, Vector.Create((short)13)),
            tok);

        Vector<short> tokIndex = FjxlSimdUtils.PrepareForU8Lookup(remapTok);
        Vector<short> huffBitsPre = FjxlSimdUtils.U8Lookup(tokIndex, rawBitsSimd);

        Vector<short> evenTok = tok & Vector.Create(unchecked((short)0xFFF));
        Vector<short> needsHighBit = above & Vector.Equals(tok, evenTok);

        Vector<short> huffBits = Vector.ConditionalSelect(
            needsHighBit,
            huffBitsPre | Vector.Create((short)128),
            huffBitsPre);

        huffBits.CopyTo(bitsOut);

        FjxlSimdUtils.U8Lookup(tokIndex, rawNbitsSimd)
            .CopyTo(nbitsOut);
    }
}
