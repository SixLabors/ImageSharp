// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

internal static class FjxlStoreSimd
{
    public static void StoreSimdUpTo8(Span<short> nbitsTok, Span<short> bitsTok, Span<short> nbitsHuff, Span<short> bitsHuff, int n, int skip, Span<Bits32> bitsOut)
    {
        FjxlBits16 bits = FjxlBits16.FromRaw(
            Vector.Create<short>(nbitsTok),
            Vector.Create<short>(bitsTok));

        FjxlBits16 huffBits = FjxlBits16.FromRaw(
            Vector.Create<short>(nbitsHuff),
            Vector.Create<short>(bitsHuff));

        bits.Interleave(huffBits);
        bits.ClipTo(n);
        bits.Skip(skip);

        bitsOut[0] = bits.Merge();
    }

    public static void StoreSimdUpTo14(Span<short> nbitsTok, Span<short> bitsTok, Span<short> nbitsHuff, Span<short> bitsHuff, int n, int skip, Span<Bits32> bitsOut)
    {
        VecPair<Vector<short>> bits = Vector.Create<short>(bitsTok).Interleave(Vector.Create<short>(bitsHuff));
        VecPair<Vector<short>> nbits = Vector.Create<short>(nbitsTok).Interleave(Vector.Create<short>(nbitsHuff));

        Bits16 low = Bits16.FromRaw(nbits.Low, bits.Low);
        Bits16 hi = Bits16.FromRaw(nbits.High, bits.High);

        int lanes = Vector<short>.Count;

        low.ClipTo(2 * n);
        low.Skip(2 * skip);

        hi.ClipTo(Math.Max(2 * n, lanes) - lanes);
        hi.Skip(Math.Max(2 * skip, lanes) - lanes);

        bitsOut[0] = low.Merge();
        bitsOut[1] = hi.Merge();
    }

    public static void StoreSimdAbove14(Span<int> nbitsTok, Span<int> bitsTok, Span<short> nbitsHuff, Span<short> bitsHuff, int n, int skip, Span<Bits32> bitsOut)
    {
        Vector<int> nbitsTokLo = Vector.Create<int>(nbitsTok);
        Vector<int> bitsTokLo = Vector.Create<int>(bitsTok);

        Vector<int> nbitsTokHi = Vector.Create<int>(nbitsTok[Vector<int>.Count..]);
        Vector<int> bitsTokHi = Vector.Create<int>(bitsTok[Vector<int>.Count..]);

        Bits32 bitsLow = Bits32.FromRaw(nbitsTokLo, bitsTokLo);
        Bits32 bitsHi = Bits32.FromRaw(nbitsTokHi, bitsTokHi);

        VecPair<Vector<int>> huffBits = FjxlSimdUtils.Upcast(Vector.Create<short>(bitsHuff));
        VecPair<Vector<int>> huffNbits = FjxlSimdUtils.Upcast(Vector.Create<short>(nbitsHuff));

        Bits32 huffLow = Bits32.FromRaw(huffNbits.Low, huffBits.Low);
        Bits32 huffHi = Bits32.FromRaw(huffNbits.High, huffBits.High);

        bitsLow.Interleave(huffLow);
        bitsLow.ClipTo(n);
        bitsLow.Skip(skip);
        bitsOut[0] = bitsLow;

        int lanes = Vector<int>.Count;

        bitsHi.Interleave(huffHi);
        bitsHi.ClipTo(Math.Max(n, lanes) - lanes);
        bitsHi.Skip(Math.Max(skip, lanes) - lanes);
        bitsOut[1] = bitsHi;
    }

    public static void StoreToWriter(ReadOnlySpan<Bits32> bits, FjxlBitWriter output, int n)
    {
        Span<ulong> nbits64 = stackalloc ulong[Bits64.Lanes * n];
        Span<ulong> bits64 = stackalloc ulong[Bits64.Lanes * n];

        bits[0].Merge().Store(nbits64, bits64);

        if (n > 1)
        {
            bits[1].Merge().Store(nbits64[Bits64.Lanes..], bits64[Bits64.Lanes..]);
        }

        if (n > 2)
        {
            bits[2].Merge().Store(nbits64[(2 * Bits64.Lanes)..], bits64[(2 * Bits64.Lanes)..]);
        }

        if (n > 3)
        {
            bits[3].Merge().Store(nbits64[(3 * Bits64.Lanes)..], bits64[(3 * Bits64.Lanes)..]);
        }

        output.WriteMultiple(nbits64, bits64, Bits64.Lanes * n);
    }
}
