// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Symbol;

internal class Av1SymbolWriter
{
    private uint low;
    private uint rng = 0x8000U;

    // Count is initialized to -9 so that it crosses zero after we've accumulated one byte + one carry bit.
    private int cnt = -9;
    private readonly Stream stream;

    public Av1SymbolWriter(Stream stream) => this.stream = stream;

    public void WriteSymbol(int symbol, uint[] probabilities, int numberOfSymbols)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(symbol, 0, nameof(symbol));
        DebugGuard.MustBeLessThan(symbol, numberOfSymbols, nameof(symbol));
        DebugGuard.IsTrue(probabilities[numberOfSymbols - 1] == 0, "Last entry in Probabilities table needs to be zero.");

        this.EncodeIntegerQ15(symbol, probabilities, numberOfSymbols);
        Av1SymbolReader.UpdateCdf(probabilities, symbol, numberOfSymbols);
    }

    public void WriteLiteral(uint value, int bitCount)
    {
        const uint p = 0x4000U; // (0x7FFFFFU - (128 << 15) + 128) >> 8;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            bool bitValue = ((value >> bit) & 0x1) > 0;
            this.EncodeBoolQ15(bitValue, p);
        }
    }

    public void Exit()
    {
        uint m;
        uint e;
        uint l;
        int c;
        int s;

        // We output the minimum number of bits that ensures that the symbols encoded
        // thus far will be decoded correctly regardless of the bits that follow.
        l = this.low;
        c = this.cnt;
        s = 10;
        m = 0x3FFFU;
        e = ((l + m) & ~m) | (m + 1);
        s += c;
        if (s > 0)
        {
            uint n = (1U << (c + 16)) - 1;
            do
            {
                this.stream.WriteByte((byte)(e >> (c + 16)));
                e &= n;
                s -= 8;
                c -= 8;
                n >>= 8;
            }
            while (s > 0);
        }
    }

    /// <summary>
    /// Encode a single binary value.
    /// </summary>
    /// <param name="val">The value to encode.</param>
    /// <param name="frequency">The probability that the value is true, scaled by 32768.</param>
    private void EncodeBoolQ15(bool val, uint frequency)
    {
        uint l;
        uint r;
        uint v;
        DebugGuard.MustBeGreaterThan(frequency, 0U, nameof(frequency));
        DebugGuard.MustBeLessThanOrEqualTo(frequency, 32768U, nameof(frequency));
        l = this.low;
        r = this.rng;
        DebugGuard.MustBeGreaterThanOrEqualTo(r, 32768U, nameof(r));
        v = ((r >> 8) * (frequency >> Av1SymbolReader.ProbabilityShift)) >> (7 - Av1SymbolReader.ProbabilityShift);
        v += Av1SymbolReader.ProbabilityMinimum;
        if (val)
        {
            l += r - v;
            r = v;
        }
        else
        {
            r -= v;
        }

        this.Normalize(l, r);
    }

    /// <summary>
    /// Encodes a symbol given an inverse cumulative distribution function(CDF) table in Q15.
    /// </summary>
    /// <param name="symbol">The value to encode.</param>
    /// <param name="probabilities">
    /// CDF_PROB_TOP minus the CDF, such that symbol s falls in the range
    /// [s > 0 ? (CDF_PROB_TOP - icdf[s - 1]) : 0, CDF_PROB_TOP - icdf[s]).
    /// The values must be monotonically non - increasing, and icdf[nsyms - 1] must be 0.
    /// </param>
    /// <param name="numberOfSymbols">
    /// The number of symbols in the alphabet.
    /// This should be at most 16.
    /// </param>
    private void EncodeIntegerQ15(int symbol, uint[] probabilities, int numberOfSymbols)
        => this.EncodeIntegerQ15(symbol > 0 ? probabilities[symbol - 1] : Av1SymbolReader.CdfProbabilityTop, probabilities[symbol], symbol, numberOfSymbols);

    private void EncodeIntegerQ15(uint lowFrequency, uint highFrequency, int symbol, int numberOfSymbols)
    {
        uint l = this.low;
        uint r = this.rng;
        int totalShift = 7 - Av1SymbolReader.ProbabilityShift - Av1SymbolReader.CdfShift;
        DebugGuard.MustBeLessThanOrEqualTo(32768U, r, nameof(r));
        DebugGuard.MustBeLessThanOrEqualTo(highFrequency, lowFrequency, nameof(highFrequency));
        DebugGuard.MustBeLessThanOrEqualTo(lowFrequency, 32768U, nameof(lowFrequency));
        DebugGuard.MustBeGreaterThanOrEqualTo(totalShift, 0, string.Empty);
        int n = numberOfSymbols - 1;
        if (lowFrequency < Av1SymbolReader.CdfProbabilityTop)
        {
            uint u;
            uint v;
            u = (uint)((((r >> 8) * (lowFrequency >> Av1SymbolReader.ProbabilityShift)) >> totalShift) +
                (Av1SymbolReader.ProbabilityMinimum * (n - (symbol - 1))));
            v = (uint)((((r >> 8) * (highFrequency >> Av1SymbolReader.ProbabilityShift)) >> totalShift) +
                (Av1SymbolReader.ProbabilityMinimum * (n - (symbol + 0))));
            l += r - u;
            r = u - v;
        }
        else
        {
            r -= (uint)((((r >> 8) * (highFrequency >> Av1SymbolReader.ProbabilityShift)) >> totalShift) +
                (Av1SymbolReader.ProbabilityMinimum * (n - (symbol + 0))));
        }

        this.Normalize(l, r);
    }

    /// <summary>
    /// Takes updated low and range values, renormalizes them so that <paramref name="rng"/>
    /// lies between 32768 and 65536 (flushing bytes from low to the pre-carry buffer if necessary),
    /// and stores them back in the encoder context.
    /// </summary>
    /// <param name="low">The new value of <see cref="low"/>.</param>
    /// <param name="rng">The new value of <see cref="rng"/>.</param>
    private void Normalize(uint low, uint rng)
    {
        int d;
        int c;
        int s;
        c = this.cnt;
        DebugGuard.MustBeLessThanOrEqualTo(rng, 65535U, nameof(rng));
        d = 15 - Av1Math.MostSignificantBit(rng);
        s = c + d;
        /*TODO: Right now we flush every time we have at least one byte available.
        Instead we should use an OdEcWindow and flush right before we're about to
        shift bits off the end of the window.
        For a 32-bit window this is about the same amount of work, but for a 64-bit
        window it should be a fair win.*/
        if (s >= 0)
        {
            uint m;

            c += 16;
            m = (1U << c) - 1;
            if (s >= 8)
            {
                this.stream.WriteByte((byte)(low >> c));
                low &= m;
                c -= 8;
                m >>= 8;
            }

            this.stream.WriteByte((byte)(low >> c));
            s = c + d - 24;
            low &= m;
        }

        this.low = low << d;
        this.rng = rng << d;
        this.cnt = s;
    }
}
