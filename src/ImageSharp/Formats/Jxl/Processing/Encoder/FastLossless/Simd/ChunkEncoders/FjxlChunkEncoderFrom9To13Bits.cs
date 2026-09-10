// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd.ChunkEncoders;

internal readonly struct FjxlChunkEncoderFrom9To13Bits
{
    public const int InputBytes = 2;

    private readonly int bitDepth;

    public FjxlChunkEncoderFrom9To13Bits(int bitDepth)
    {
        DebugGuard.MustBeLessThanOrEqualTo(bitDepth, 13, nameof(bitDepth));
        DebugGuard.MustBeGreaterThanOrEqualTo(bitDepth, 9, nameof(bitDepth));

        this.bitDepth = bitDepth;
    }

    public static ReadOnlySpan<byte> MinRawLength =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    public static ReadOnlySpan<byte> MaxRawLength =>
    [
        8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 8, 10
    ];

    public static int MaxEncodedBitsPerSample() => 22;

    public static void PrepareForSimd(ReadOnlySpan<byte> nbits, ReadOnlySpan<byte> bits, int n, Span<byte> nbitsSimd, Span<byte> bitsSimd)
    {
        DebugGuard.MustBeLessThanOrEqualTo(n, 16, nameof(n));

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
            HuffmanSimdUpTo13(token, rawNBitsSimd, rawBitsSimd, nbitsHuff, bitsHuff);

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

    public int NumSymbols(bool doingYcocgOrLargePalette) => this.bitDepth + (doingYcocgOrLargePalette ? 3 : 2);
}
