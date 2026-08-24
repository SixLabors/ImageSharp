// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Writes AV1 literals and adaptively coded symbols to a range-coded byte sequence.
/// </summary>
internal class Av1SymbolWriter : IDisposable
{
    /// <summary>
    /// The lower endpoint of the current coding interval.
    /// </summary>
    private uint low;

    /// <summary>
    /// The width of the current normalized coding interval.
    /// </summary>
    private uint rng = 0x8000U;

    /// <summary>
    /// The number of accumulated bits relative to the next byte-and-carry flush boundary.
    /// </summary>
    /// <remarks>
    /// The initial value of -9 crosses zero after one output byte and its carry bit have accumulated.
    /// </remarks>
    private int cnt = -9;

    /// <summary>
    /// The configuration that supplies output allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The pre-carry output values accumulated during renormalization.
    /// </summary>
    private readonly AutoExpandingMemory<ushort> memory;

    /// <summary>
    /// The next pre-carry output position.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolWriter"/> class with an estimated output size.
    /// </summary>
    /// <param name="configuration">The configuration that supplies output allocation.</param>
    /// <param name="initialSize">The estimated encoded size in bytes.</param>
    public Av1SymbolWriter(Configuration configuration, int initialSize)
    {
        this.configuration = configuration;
        this.memory = new AutoExpandingMemory<ushort>(configuration, (initialSize + 1) >> 1);
    }

    /// <summary>
    /// Releases the expandable pre-carry buffer.
    /// </summary>
    public void Dispose() => this.memory.Dispose();

    /// <summary>
    /// Writes one binary symbol and adapts its distribution.
    /// </summary>
    /// <param name="symbol">The binary symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the binary alphabet.</param>
    public void WriteSymbol(bool symbol, Av1Distribution distribution)
        => this.WriteSymbol(symbol ? 1 : 0, distribution);

    /// <summary>
    /// Writes one symbol and adapts its distribution.
    /// </summary>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the symbol alphabet.</param>
    public void WriteSymbol(int symbol, Av1Distribution distribution)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(symbol, 0, nameof(symbol));
        DebugGuard.MustBeLessThan(symbol, distribution.NumberOfSymbols, nameof(symbol));
        DebugGuard.IsTrue(distribution[distribution.NumberOfSymbols - 1] == 0, "Last entry in Probabilities table needs to be zero.");

        this.EncodeIntegerQ15(symbol, distribution);
        distribution.Update(symbol);
    }

    /// <summary>
    /// Writes one equiprobable literal bit.
    /// </summary>
    /// <param name="value">The literal bit.</param>
    public void WriteLiteral(bool value) => this.WriteLiteral(value ? 1u : 0u, 1);

    /// <summary>
    /// Writes the requested low-order bits in most-significant-bit-first order.
    /// </summary>
    /// <param name="value">The unsigned literal value.</param>
    /// <param name="bitCount">The number of low-order bits to write.</param>
    public void WriteLiteral(uint value, int bitCount)
    {
        const uint p = 0x4000U; // (0x7FFFFFU - (128 << 15) + 128) >> 8;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            bool bitValue = ((value >> bit) & 0x1) > 0;
            this.EncodeBoolQ15(bitValue, p);
        }
    }

    /// <summary>
    /// Terminates the range-coded sequence and propagates pending carries into an owned byte buffer.
    /// </summary>
    /// <returns>An owner containing the shortest byte sequence that preserves every encoded symbol.</returns>
    public IMemoryOwner<byte> Exit()
    {
        // Round the low endpoint into the current interval so the emitted prefix selects every symbol encoded so far
        // regardless of the bits that follow it.
        uint l = this.low;
        int c = this.cnt;
        int pos = this.position;
        int s = 10;
        uint m = 0x3FFFU;
        uint e = ((l + m) & ~m) | (m + 1);
        s += c;
        Span<ushort> buffer = this.memory.GetSpan(this.position + ((s + 7) >> 3));
        if (s > 0)
        {
            uint n = (1U << (c + 16)) - 1;
            do
            {
                buffer[pos] = (ushort)(e >> (c + 16));
                pos++;
                e &= n;
                s -= 8;
                c -= 8;
                n >>= 8;
            }
            while (s > 0);
        }

        c = Math.Max((s + 7) >> 3, 0);
        IMemoryOwner<byte> output = this.configuration.MemoryAllocator.Allocate<byte>(pos + c);

        // Pre-carry values use 16-bit elements so a byte plus a propagated carry can coexist. Walking backwards folds
        // each carry into the preceding byte without shifting the buffered sequence.
        Span<byte> outputSlice = output.GetSpan()[(output.Length() - pos)..];
        c = 0;
        while (pos > 0)
        {
            pos--;
            c = buffer[pos] + c;
            outputSlice[pos] = (byte)c;
            c >>= 8;
        }

        return output;
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

        // Reduce the Q15 frequency to the range-coder multiplication precision and retain a nonzero interval for
        // both outcomes. Av1SymbolReader applies the identical rounding model.
        v = ((r >> 8) * (frequency >> Av1Distribution.ProbabilityShift)) >> (7 - Av1Distribution.ProbabilityShift);
        v += Av1Distribution.ProbabilityMinimum;
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
    /// <param name="distribution">
    /// CDF_PROB_TOP minus the CDF, such that symbol s falls in the range
    /// [s > 0 ? (CDF_PROB_TOP - icdf[s - 1]) : 0, CDF_PROB_TOP - icdf[s]).
    /// The values must be monotonically non - increasing, and icdf[nsyms - 1] must be 0.
    /// </param>
    private void EncodeIntegerQ15(int symbol, Av1Distribution distribution)
        => this.EncodeIntegerQ15(symbol > 0 ? distribution[symbol - 1] : Av1Distribution.ProbabilityTop, distribution[symbol], symbol, distribution.NumberOfSymbols);

    /// <summary>
    /// Narrows the coding interval to one symbol's inverse-cumulative bounds.
    /// </summary>
    /// <param name="lowFrequency">The inverse cumulative threshold preceding the symbol.</param>
    /// <param name="highFrequency">The inverse cumulative threshold following the symbol.</param>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="numberOfSymbols">The size of the symbol alphabet.</param>
    private void EncodeIntegerQ15(uint lowFrequency, uint highFrequency, int symbol, int numberOfSymbols)
    {
        const int totalShift = 7 - Av1Distribution.ProbabilityShift - Av1Distribution.CdfShift;
        uint l = this.low;
        uint r = this.rng;
        DebugGuard.MustBeLessThanOrEqualTo(32768U, r, nameof(r));
        DebugGuard.MustBeLessThanOrEqualTo(highFrequency, lowFrequency, nameof(highFrequency));
        DebugGuard.MustBeLessThanOrEqualTo(lowFrequency, 32768U, nameof(lowFrequency));
        DebugGuard.MustBeGreaterThanOrEqualTo(totalShift, 0, nameof(totalShift));
        int n = numberOfSymbols - 1;
        if (lowFrequency < Av1Distribution.ProbabilityTop)
        {
            uint u;
            uint v;
            u = (uint)((((r >> 8) * (lowFrequency >> Av1Distribution.ProbabilityShift)) >> totalShift) +
                (Av1Distribution.ProbabilityMinimum * (n - (symbol - 1))));
            v = (uint)((((r >> 8) * (highFrequency >> Av1Distribution.ProbabilityShift)) >> totalShift) +
                (Av1Distribution.ProbabilityMinimum * (n - symbol)));
            l += r - u;
            r = u - v;
        }
        else
        {
            r -= (uint)((((r >> 8) * (highFrequency >> Av1Distribution.ProbabilityShift)) >> totalShift) +
                (Av1Distribution.ProbabilityMinimum * (n - symbol)));
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

        // The 32-bit low endpoint is flushed whenever a byte becomes available. Retaining pre-carry values as
        // ushort elements defers carry propagation until Exit without requiring a separate wider coding window.
        if (s >= 0)
        {
            uint m;
            Span<ushort> buffer = this.memory.GetSpan(this.position + 2);

            c += 16;
            m = (1U << c) - 1;
            if (s >= 8)
            {
                buffer[this.position] = (ushort)(low >> c);
                this.position++;
                low &= m;
                c -= 8;
                m >>= 8;
            }

            buffer[this.position] = (ushort)(low >> c);
            this.position++;
            s = c + d - 24;
            low &= m;
        }

        this.low = low << d;
        this.rng = rng << d;
        this.cnt = s;
    }
}
