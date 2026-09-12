// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd.ChunkEncoders;

internal readonly struct FjxlChunkEncoderExactly14Bits
{
    public const int BitDepth = 14;
    public const int InputBytes = 2;

    public static ReadOnlySpan<byte> MinRawLength =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 8, 8, 7
    ];

    public static ReadOnlySpan<byte> MaxRawLength =>
    [
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8, 8, 10
    ];

    public static int MaxEncodedBitsPerSample() => 23;

    public static void PrepareForSimd(ReadOnlySpan<byte> nbits, ReadOnlySpan<byte> bits, Span<byte> nbitsSimd, Span<byte> bitsSimd)
    {
        CheckHuffmanBitsSimd(bits[15], nbits[15], bits[16], nbits[16]);

        nbits[..16].CopyTo(nbitsSimd);
        bits[..16].CopyTo(bitsSimd);
    }

    public static void EncodeChunkSimd(Span<ushort> residuals, int n, int skip, ReadOnlySpan<byte> rawNBitsSimd, ReadOnlySpan<byte> rawBitsSimd, ref FjxlBitWriter output)
    {
        Span<FjxlBits32> bits32 = stackalloc FjxlBits32[2 * ChunkSize / FjxlSimdVec16.Lanes];
        Span<ushort> bits = stackalloc ushort[FjxlSimdVec16.Lanes];
        Span<ushort> nbits = stackalloc ushort[FjxlSimdVec16.Lanes];
        Span<ushort> bitsHuff = stackalloc ushort[FjxlSimdVec16.Lanes];
        Span<ushort> nbitsHuff = stackalloc ushort[FjxlSimdVec16.Lanes];
        Span<ushort> token = stackalloc ushort[FjxlSimdVec16.Lanes];

        for (int i = 0; i < ChunkSize; i += FjxlSimdVec16.Lanes)
        {
            TokenizeSimd(residuals[i..], token, nbits, bits);
            HuffmanSimd14(token, rawNBitsSimd, rawBitsSimd, nbitsHuff, bitsHuff);

            StoreSimdUpTo14(
                nbits,
                bits,
                nbitsHuff,
                bitsHuff,
                Math.Max(n, i) - i,
                Math.Max(skip, i) - i,
                bits32[(2 * i / FjxlSimdVec16.Lanes)..]);
        }

        StoreToWriter<FjxlBits32, 2 * ChunkSize / FjxlSimdVec16.Lanes>(bits32, ref output);
    }

    public static int NumSymbols() => 17;
}
