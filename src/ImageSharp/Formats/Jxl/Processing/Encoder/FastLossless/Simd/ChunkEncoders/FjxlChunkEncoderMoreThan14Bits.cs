// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd.ChunkEncoders;

internal readonly struct FjxlChunkEncoderMoreThan14Bits
{
    public const int InputBytes = 2;

    private readonly int bitDepth;

    public FjxlChunkEncoderMoreThan14Bits(int bitDepth)
    {
        DebugGuard.MustBeGreaterThan(bitDepth, 14, nameof(bitDepth));
        DebugGuard.MustBeLessThanOrEqualTo(bitDepth, 16, nameof(bitDepth));

        this.bitDepth = bitDepth;
    }

    public static ReadOnlySpan<byte> MinRawLength =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 8, 8, 8, 8, 8, 8, 7
    ];

    public static ReadOnlySpan<byte> MaxRawLength =>
    [
        7, 7, 7, 7, 7, 7, 7, 7, 7, 7,
        7, 7, 7, 8, 8, 8, 8, 8, 8, 10
    ];

    public static int MaxEncodedBitsPerSample() => 25;

    public static void PrepareForSimd(ReadOnlySpan<byte> nbits, ReadOnlySpan<byte> bits, Span<byte> nbitsSimd, Span<byte> bitsSimd)
    {
        CheckHuffmanBitsSimd(bits[13], nbits[13], bits[14], nbits[14]);
        CheckHuffmanBitsSimd(bits[15], nbits[15], bits[16], nbits[16]);
        CheckHuffmanBitsSimd(bits[17], nbits[17], bits[18], nbits[18]);

        nbits[..14].CopyTo(nbitsSimd);
        bits[..14].CopyTo(bitsSimd);

        nbitsSimd[14] = nbits[15];
        bitsSimd[14] = bits[15];
        nbitsSimd[15] = nbits[17];
        bitsSimd[15] = bits[17];
    }

    public static void EncodeChunkSimd(Span<uint> residuals, int n, int skip, ReadOnlySpan<byte> rawNBitsSimd, ReadOnlySpan<byte> rawBitsSimd, ref FjxlBitWriter output)
    {
        Span<FjxlBits32> bits32 = stackalloc FjxlBits32[2 * ChunkSize / FjxlSimdVec16.Lanes];

        Span<uint> bufferU32 = stackalloc uint[FjxlSimdVec16.Lanes * 2];
        Span<uint> bits = bufferU32.Slice(0, FjxlSimdVec16.Lanes);
        Span<uint> nbits = bufferU32.Slice(FjxlSimdVec16, FjxlSimdVec16.Lanes);

        Span<ushort> bufferU16 = stackalloc uint[FjxlSimdVec16.Lanes * 3];
        Span<ushort> bitsHuff = bufferU16.Slice(0, FjxlSimdVec16.Lanes);
        Span<ushort> nbitsHuff = bufferU16.Slice(FjxlSimdVec16.Lanes, FjxlSimdVec16.Lanes);
        Span<ushort> token = bufferU16.Slice(FjxlSimdVec16.Lanes * 2, FjxlSimdVec16.Lanes);

        for (int i = 0; i < ChunkSize; i += FjxlSimdVec16.Lanes)
        {
            TokenizeSimd(residuals[i..], token, nbits, bits);
            HuffmanSimdAbove14(token, rawNBitsSimd, rawBitsSimd, nbitsHuff, bitsHuff);

            StoreSimdAbove14(
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

    public static int NumSymbols() => 19;
}
