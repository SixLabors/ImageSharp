// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

internal ref struct Av1SymbolReader
{
    private const int DecoderWindowsSize = 32;
    private const int LotsOfBits = 0x4000;

    private readonly Span<byte> buffer;
    private int position;

    /*
     * The difference between the high end of the current range, (low + rng), and
     * the coded value, minus 1.
     * This stores up to OD_EC_WINDOW_SIZE bits of that difference, but the
     * decoder only uses the top 16 bits of the window to decode the next symbol.
     * As we shift up during renormalization, if we don't have enough bits left in
     * the window to fill the top 16, we'll read in more bits of the coded
     * value.
     */
    private uint difference;

    // The number of values in the current range.
    private uint range;

    // The number of bits in the current value.
    private int count;

    public Av1SymbolReader(Span<byte> span)
    {
        this.buffer = span;
        this.position = 0;
        this.difference = (1U << (DecoderWindowsSize - 1)) - 1;
        this.range = 0x8000;
        this.count = -15;
        this.Refill();
    }

    public int ReadSymbol(Av1Distribution distribution)
    {
        int value = this.DecodeIntegerQ15(distribution);

        // UpdateCdf(probabilities, value, numberOfSymbols);
        distribution.Update(value);
        return value;
    }

    public int ReadLiteral(int bitCount)
    {
        const uint prob = (0x7FFFFFU - (128 << 15) + 128) >> 8;
        int literal = 0;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            if (this.DecodeBoolQ15(prob))
            {
                literal |= 1 << bit;
            }
        }

        return literal;
    }

    /// <summary>
    /// Decode a single binary value.
    /// </summary>
    /// <param name="frequency">The probability that the bit is one, scaled by 32768.</param>
    private bool DecodeBoolQ15(uint frequency)
    {
        uint dif;
        uint vw;
        uint range;
        uint newRange;
        uint v;
        bool ret;

        // assert(0 < f);
        // assert(f < 32768U);
        dif = this.difference;
        range = this.range;

        // assert(dif >> (DecoderWindowsSize - 16) < r);
        // assert(32768U <= r);
        v = ((range >> 8) * (frequency >> Av1Distribution.ProbabilityShift)) >> (7 - Av1Distribution.ProbabilityShift);
        v += Av1Distribution.ProbabilityMinimum;
        vw = v << (DecoderWindowsSize - 16);
        ret = true;
        newRange = v;
        if (dif >= vw)
        {
            newRange = range - v;
            dif -= vw;
            ret = false;
        }

        this.Normalize(dif, newRange);
        return ret;
    }

    /// <summary>
    /// Decodes a symbol given an inverse cumulative distribution function(CDF) table in Q15.
    /// </summary>
    /// <param name="distribution">
    /// CDF_PROB_TOP minus the CDF, such that symbol s falls in the range
    /// [s > 0 ? (CDF_PROB_TOP - icdf[s - 1]) : 0, CDF_PROB_TOP - icdf[s]).
    /// The values must be monotonically non - increasing, and icdf[nsyms - 1] must be 0.
    /// </param>
    /// <returns>The decoded symbol.</returns>
    private int DecodeIntegerQ15(Av1Distribution distribution)
    {
        uint c;
        uint u;
        uint v;
        int ret;

        uint dif = this.difference;
        uint r = this.range;
        int n = distribution.NumberOfSymbols - 1;

        DebugGuard.MustBeLessThan(dif >> (DecoderWindowsSize - 16), r, nameof(r));
        DebugGuard.IsTrue(distribution[n] == 0, "Last value in probability array needs to be zero.");
        DebugGuard.MustBeGreaterThanOrEqualTo(r, 32768U, nameof(r));
        DebugGuard.MustBeGreaterThanOrEqualTo(7 - Av1Distribution.ProbabilityShift - Av1Distribution.CdfShift, 0, nameof(Av1Distribution.CdfShift));
        c = dif >> (DecoderWindowsSize - 16);
        v = r;
        ret = -1;
        do
        {
            u = v;
            v = ((r >> 8) * (distribution[++ret] >> Av1Distribution.ProbabilityShift)) >> (7 - Av1Distribution.ProbabilityShift - Av1Distribution.CdfShift);
            v += (uint)(Av1Distribution.ProbabilityMinimum * (n - ret));
        }
        while (c < v);

        DebugGuard.MustBeLessThan(v, u, nameof(v));
        DebugGuard.MustBeLessThanOrEqualTo(u, r, nameof(u));
        r = u - v;
        dif -= v << (DecoderWindowsSize - 16);
        this.Normalize(dif, r);
        return ret;
    }

    /// <summary>
    /// Takes updated dif and range values, renormalizes them so that
    /// <paramref name="rng"/> has value between 32768 and 65536 (reading more bytes from the stream into dif if
    /// necessary), and stores them back in the decoder context.
    /// </summary>
    private void Normalize(uint dif, uint rng)
    {
        int d;

        // assert(rng <= 65535U);
        /*The number of leading zeros in the 16-bit binary representation of rng.*/
        d = 15 - Av1Math.MostSignificantBit(rng);
        /*d bits in dec->dif are consumed.*/
        this.count -= d;
        /*This is equivalent to shifting in 1's instead of 0's.*/
        this.difference = ((dif + 1) << d) - 1;
        this.range = rng << d;
        if (this.count < 0)
        {
            this.Refill();
        }
    }

    private void Refill()
    {
        int s;
        uint dif = this.difference;
        int cnt = this.count;
        int position = this.position;
        int end = this.buffer.Length;
        s = DecoderWindowsSize - 9 - (cnt + 15);
        for (; s >= 0 && position < end; s -= 8, position++)
        {
            /*Each time a byte is inserted into the window (dif), bptr advances and cnt
           is incremented by 8, so the total number of consumed bits (the return
           value of od_ec_dec_tell) does not change.*/
            DebugGuard.MustBeLessThan(s, DecoderWindowsSize - 8, nameof(s));
            dif ^= (uint)this.buffer[position] << s;
            cnt += 8;
        }

        if (position >= end)
        {
            /*
             * We've reached the end of the buffer. It is perfectly valid for us to need
             * to fill the window with additional bits past the end of the buffer (and
             * this happens in normal operation). These bits should all just be taken
             * as zero. But we cannot increment bptr past 'end' (this is undefined
             * behavior), so we start to increment dec->tell_offs. We also don't want
             * to keep testing bptr against 'end', so we set cnt to OD_EC_LOTS_OF_BITS
             * and adjust dec->tell_offs so that the total number of unconsumed bits in
             * the window (dec->cnt - dec->tell_offs) does not change. This effectively
             * puts lots of zero bits into the window, and means we won't try to refill
             * it from the buffer for a very long time (at which point we'll put lots
             * of zero bits into the window again).
             */
            cnt = LotsOfBits;
        }

        this.difference = dif;
        this.count = cnt;
        this.position = position;
    }
}
