// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Reads AV1 literals and adaptively coded symbols from one bounded entropy-coded byte span.
/// </summary>
internal ref struct Av1SymbolReader
{
    /// <summary>
    /// The number of bits in the range-decoder code-value window.
    /// </summary>
    private const int DecoderWindowsSize = 32;

    /// <summary>
    /// The synthetic count used after the bounded input has been exhausted and zero padding begins.
    /// </summary>
    private const int LotsOfBits = 0x4000;

    /// <summary>
    /// The bounded entropy-coded bytes available to this reader.
    /// </summary>
    private readonly Span<byte> buffer;

    /// <summary>
    /// Indicates whether decoded symbols adapt their distributions.
    /// </summary>
    private readonly bool updateCdf;

    /// <summary>
    /// The next byte position to load into the code-value window.
    /// </summary>
    private int position;

    /// <summary>
    /// The difference between the upper end of the current range and the coded value, minus one.
    /// </summary>
    /// <remarks>
    /// The decoder compares the upper 16 bits. Renormalization shifts consumed bits out and refills the lower portion
    /// from <see cref="buffer"/> so the comparison remains aligned with <see cref="range"/>.
    /// </remarks>
    private uint difference;

    /// <summary>
    /// The number of code values in the current normalized interval.
    /// </summary>
    private uint range;

    /// <summary>
    /// The number of buffered bits below the 16-bit comparison window.
    /// </summary>
    private int count;

    /// <summary>
    /// The adjustment that preserves the logical consumed-bit count after the reader enters implicit zero padding.
    /// </summary>
    private int tellOffset;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolReader"/> struct over one entropy-coded span.
    /// </summary>
    /// <param name="span">The bounded entropy-coded bytes.</param>
    /// <param name="updateCdf">A value indicating whether decoded symbols adapt their distributions.</param>
    public Av1SymbolReader(Span<byte> span, bool updateCdf = true)
    {
        this.buffer = span;
        this.updateCdf = updateCdf;
        this.position = 0;
        this.difference = (1U << (DecoderWindowsSize - 1)) - 1;
        this.range = 0x8000;
        this.count = -15;
        this.tellOffset = 10 - (DecoderWindowsSize - 8);
        this.Refill();
    }

    /// <summary>
    /// Validates that range decoding remained within the bounded tile payload and ended at the required trailing-one bit.
    /// </summary>
    public void ValidateTrailingBits()
    {
        int consumedBitCount = this.GetConsumedBitCount();
        int consumedByteCount = (consumedBitCount + 7) >> 3;
        if (consumedByteCount > this.buffer.Length)
        {
            throw new InvalidImageContentException("The AV1 tile entropy stream is truncated.");
        }

        // The last consumed byte must contain one trailing-one bit at the exact stop position of the range decoder, followed only by
        // zero bits. AV1 decoders do this check on the bounded stream after they decode a tile.
        int trailingOneBit = 128 >> ((consumedBitCount - 1) & 7);
        int trailingBitMask = (trailingOneBit << 1) - 1;
        if ((this.buffer[consumedByteCount - 1] & trailingBitMask) != trailingOneBit)
        {
            throw new InvalidImageContentException("The AV1 tile entropy stream has invalid trailing bits.");
        }

        for (int i = consumedByteCount; i < this.buffer.Length; i++)
        {
            if (this.buffer[i] != 0)
            {
                throw new InvalidImageContentException("The AV1 tile entropy stream has nonzero padding bytes.");
            }
        }
    }

    /// <summary>
    /// Reads one symbol and adapts its distribution when CDF updates are enabled.
    /// </summary>
    /// <param name="distribution">The inverse cumulative distribution for the symbol alphabet.</param>
    /// <returns>The decoded zero-based symbol.</returns>
    public int ReadSymbol(Av1Distribution distribution)
    {
        int value = this.DecodeIntegerQ15(distribution);

        // When disable_cdf_update is set, every tile distribution stays fixed. Range decoding does not change.
        if (this.updateCdf)
        {
            distribution.Update(value);
        }

        return value;
    }

    /// <summary>
    /// Reads one non-adaptive binary symbol using the supplied Q15 probability for <see langword="true"/>.
    /// </summary>
    /// <param name="frequency">The probability that the symbol is <see langword="true"/>, scaled by 32768.</param>
    /// <returns>The decoded binary symbol.</returns>
    public bool ReadBoolean(uint frequency)
    {
        bool value = this.DecodeBoolQ15(frequency);
        return value;
    }

    /// <summary>
    /// Reads an unsigned literal in most-significant-bit-first order.
    /// </summary>
    /// <param name="bitCount">The number of literal bits to read.</param>
    /// <returns>The decoded literal.</returns>
    public int ReadLiteral(int bitCount)
    {
        const uint prob = (0x7FFFFFU - (128 << 15) + 128) >> 8;
        int literal = 0;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            bool bitValue = this.DecodeBoolQ15(prob);
            if (bitValue)
            {
                literal |= 1 << bit;
            }
        }

        return literal;
    }

    /// <summary>
    /// Decodes one binary value with a fixed Q15 probability.
    /// </summary>
    /// <param name="frequency">The probability that the bit is one, scaled by 32768.</param>
    /// <returns>The decoded binary value.</returns>
    private bool DecodeBoolQ15(uint frequency)
    {
        uint dif;
        uint vw;
        uint range;
        uint newRange;
        uint v;
        bool ret;

        dif = this.difference;
        range = this.range;

        // The Q15 frequency drops to the multiplication precision of the range coder. Then both outcomes get a minimum interval.
        // Av1SymbolWriter uses the same rounding model.
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
    /// Decodes a symbol with an inverse cumulative distribution function (CDF) table in Q15.
    /// </summary>
    /// <param name="distribution">
    /// The inverse CDF: <see cref="Av1Distribution.ProbabilityTop"/> minus the CDF. Symbol s covers the range from
    /// (s &gt; 0 ? ProbabilityTop - icdf[s - 1] : 0) to ProbabilityTop - icdf[s], with the upper bound excluded.
    /// The values must not increase, and the last value must be 0.
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
    /// Renormalizes updated difference and range values so that <paramref name="rng"/> lies between 32768 and 65536, and stores them
    /// in the decoder state. When the window runs out of buffered bits, it reads more bytes from the stream.
    /// </summary>
    /// <param name="dif">The updated code-value difference.</param>
    /// <param name="rng">The updated coding interval width.</param>
    private void Normalize(uint dif, uint rng)
    {
        // Shifting by the leading-zero count restores the interval to [32768, 65536) and consumes the same number of
        // code-value bits. Adding one before the shift preserves the decoder's difference-minus-one representation.
        int d = 15 - Av1Math.MostSignificantBit(rng);
        this.count -= d;
        this.difference = ((dif + 1) << d) - 1;
        this.range = rng << d;
        if (this.count < 0)
        {
            this.Refill();
        }
    }

    /// <summary>
    /// Loads whole bytes into the lower portion of the code-value window after renormalization.
    /// </summary>
    private void Refill()
    {
        uint dif = this.difference;
        int cnt = this.count;
        int position = this.position;
        int end = this.buffer.Length;
        int s = DecoderWindowsSize - 9 - (cnt + 15);
        for (; s >= 0 && position < end; s -= 8, position++)
        {
            // XOR inserts a source byte into the difference-minus-one representation. Advancing both the byte
            // position and buffered-bit count leaves the logical number of consumed bits unchanged.
            DebugGuard.MustBeLessThan(s, DecoderWindowsSize - 8, nameof(s));
            dif ^= (uint)this.buffer[position] << s;
            cnt += 8;
        }

        if (position >= end)
        {
            // AV1 range decoding lets the last interval consume implicit zero padding. A large count models that padding, so the
            // reader does not try to refill again. The tellOffset field keeps the previous count. As a result, the logical position
            // continues past the bounded source, and the reader can still detect a truncated payload.
            this.tellOffset += LotsOfBits - cnt;
            cnt = LotsOfBits;
        }

        this.difference = dif;
        this.count = cnt;
        this.position = position;
    }

    /// <summary>
    /// Gets the number of entropy bits consumed from the bounded tile payload, including the initial range-coder bit.
    /// </summary>
    /// <returns>The logical consumed-bit count.</returns>
    private readonly int GetConsumedBitCount()
        => (this.position * 8) - this.count + this.tellOffset;
}
