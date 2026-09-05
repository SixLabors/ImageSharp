// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Writes AV1 literals and adaptively coded symbols to a range-coded byte sequence.
/// </summary>
internal sealed class Av1SymbolWriter : IDisposable
{
    /// <summary>
    /// The normalized range before the first symbol narrows the coding interval.
    /// </summary>
    private const uint InitialRange = 0x8000U;

    /// <summary>
    /// The initial bit count that crosses the first byte-and-carry flush boundary after one output byte.
    /// </summary>
    private const int InitialCount = -9;

    /// <summary>
    /// The lower endpoint of the current coding interval.
    /// </summary>
    private ulong low;

    /// <summary>
    /// The width of the current normalized coding interval.
    /// </summary>
    private uint rng = InitialRange;

    /// <summary>
    /// The number of accumulated bits relative to the next byte-and-carry flush boundary.
    /// </summary>
    /// <remarks>
    /// The initial value of -9 crosses zero after one output byte and its carry bit have accumulated.
    /// </remarks>
    private int cnt = InitialCount;

    /// <summary>
    /// The configuration that supplies output allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// The owner of the fixed output buffer shared by consecutively encoded tiles.
    /// </summary>
    private readonly IMemoryOwner<byte> bufferOwner;

    /// <summary>
    /// The complete requested output allocation, including every consecutively encoded tile.
    /// </summary>
    private readonly Memory<byte> outputBuffer;

    /// <summary>
    /// The requested output range, excluding any excess capacity returned by a pooling allocator.
    /// </summary>
    private Memory<byte> buffer;

    /// <summary>
    /// Indicates whether encoded symbols adapt their distributions.
    /// </summary>
    private readonly bool updateCdf;

    /// <summary>
    /// The next output byte position.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolWriter"/> class with a bounded output size.
    /// </summary>
    /// <param name="configuration">The configuration that supplies output allocation.</param>
    /// <param name="bufferLength">The complete fixed output allocation length in bytes.</param>
    /// <param name="updateCdf">A value indicating whether encoded symbols adapt their distributions.</param>
    public Av1SymbolWriter(Configuration configuration, int bufferLength, bool updateCdf)
    {
        this.configuration = configuration;
        this.bufferOwner = configuration.MemoryAllocator.Allocate<byte>(bufferLength);
        this.outputBuffer = this.bufferOwner.Memory[..bufferLength];
        this.buffer = this.outputBuffer;
        this.updateCdf = updateCdf;
    }

    /// <summary>
    /// Restores the initial range-coder state while retaining the bounded output allocation.
    /// </summary>
    public void Reset() => this.Reset(0);

    /// <summary>
    /// Restores the initial range-coder state and begins writing at an offset in the retained output allocation.
    /// </summary>
    /// <param name="outputOffset">The first byte available to the next range-coded tile.</param>
    public void Reset(int outputOffset)
    {
        this.buffer = this.outputBuffer[outputOffset..];
        this.low = 0;
        this.rng = InitialRange;
        this.cnt = InitialCount;
        this.position = 0;
    }

    /// <summary>
    /// Releases the tile output buffer.
    /// </summary>
    public void Dispose() => this.bufferOwner.Dispose();

    /// <summary>
    /// Writes one binary symbol and adapts its distribution when CDF updates are enabled.
    /// </summary>
    /// <param name="symbol">The binary symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the binary alphabet.</param>
    public void WriteSymbol(bool symbol, Av1Distribution distribution)
        => this.WriteSymbol(symbol ? 1 : 0, distribution);

    /// <summary>
    /// Writes one symbol and adapts its distribution when CDF updates are enabled.
    /// </summary>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the symbol alphabet.</param>
    public void WriteSymbol(int symbol, Av1Distribution distribution)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(symbol, 0, nameof(symbol));
        DebugGuard.MustBeLessThan(symbol, distribution.NumberOfSymbols, nameof(symbol));
        DebugGuard.IsTrue(distribution[distribution.NumberOfSymbols - 1] == 0, "Last entry in Probabilities table needs to be zero.");

        this.EncodeIntegerQ15(symbol, distribution);

        // disable_cdf_update freezes every tile distribution while leaving range encoding unchanged.
        if (this.updateCdf)
        {
            distribution.Update(symbol);
        }
    }

    /// <summary>
    /// Writes one non-adaptive binary symbol using the supplied Q15 probability for <see langword="true"/>.
    /// </summary>
    /// <param name="value">The binary symbol.</param>
    /// <param name="frequency">The probability that the symbol is <see langword="true"/>, scaled by 32768.</param>
    public void WriteBoolean(bool value, uint frequency) => this.EncodeBoolQ15(value, frequency);

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
        int length = this.FinalizeRange();
        IMemoryOwner<byte> output = this.configuration.MemoryAllocator.Allocate<byte>(length);
        this.buffer.Span[..length].CopyTo(output.Memory.Span);

        return output;
    }

    /// <summary>
    /// Finalizes the range-coded sequence and exposes its encoded prefix without copying.
    /// </summary>
    /// <param name="length">The number of encoded bytes in the returned memory.</param>
    /// <returns>The encoded prefix, valid until this writer is reset or disposed.</returns>
    public ReadOnlyMemory<byte> Exit(out int length)
    {
        length = this.FinalizeRange();
        return this.buffer[..length];
    }

    /// <summary>
    /// Exposes a prefix containing consecutively encoded tiles without copying their bytes.
    /// </summary>
    /// <param name="length">The number of bytes in the prefix.</param>
    /// <returns>The encoded prefix, valid until this writer is reset to offset zero or disposed.</returns>
    public ReadOnlyMemory<byte> GetOutput(int length) => this.outputBuffer[..length];

    /// <summary>
    /// Terminates the range-coded sequence in the current output allocation.
    /// </summary>
    /// <returns>The number of encoded bytes in the allocation.</returns>
    private int FinalizeRange()
    {
        // Round the low endpoint into the current interval so the emitted prefix selects every symbol encoded so far,
        // regardless of the bits that follow it.
        ulong l = this.low;
        int c = this.cnt;
        int pos = this.position;
        int s = 10;
        ulong m = 0x3FFFU;
        ulong e = ((l + m) & ~m) | (m + 1);
        s += c;
        int pendingByteCount = Math.Max((s + 7) >> 3, 0);
        Span<byte> buffer = this.buffer.Span[..(pos + pendingByteCount)];
        if (s > 0)
        {
            ulong n = (1UL << (c + 16)) - 1;
            do
            {
                ushort value = (ushort)(e >> (c + 16));
                buffer[pos] = (byte)value;
                if ((value & 0x100) != 0)
                {
                    PropagateCarryBackward(buffer, pos - 1);
                }

                pos++;
                e &= n;
                s -= 8;
                c -= 8;
                n >>= 8;
            }
            while (s > 0);
        }

        return pos;
    }

    /// <summary>
    /// Encode a single binary value.
    /// </summary>
    /// <param name="val">The value to encode.</param>
    /// <param name="frequency">The probability that the value is true, scaled by 32768.</param>
    private void EncodeBoolQ15(bool val, uint frequency)
    {
        ulong l;
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
        ulong l = this.low;
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
    private void Normalize(ulong low, uint rng)
    {
        int c = this.cnt;
        DebugGuard.MustBeLessThanOrEqualTo(rng, 65535U, nameof(rng));
        int d = 15 - Av1Math.MostSignificantBit(rng);
        int s = c + d;

        // Keeping 16 bits free for the next symbol allows the 64-bit coding window to flush up to eight completed
        // bytes together while preserving one carry bit.
        if (s >= 40)
        {
            Span<byte> buffer = this.buffer.Span[..(this.position + sizeof(ulong))];
            int readyByteCount = (s >> 3) + 1;
            c += 24 - (readyByteCount << 3);
            ulong output = low >> c;
            low &= (1UL << c) - 1;
            ulong carryMask = 1UL << (readyByteCount << 3);
            bool hasCarry = (output & carryMask) != 0;
            output &= carryMask - 1;

            // Writing one big-endian word avoids a byte-at-a-time hot loop. Only readyByteCount bytes become part
            // of the logical output; the following bytes are overwritten by the next flush.
            BinaryPrimitives.WriteUInt64BigEndian(
                buffer.Slice(this.position, sizeof(ulong)),
                output << ((sizeof(ulong) - readyByteCount) << 3));

            if (hasCarry)
            {
                PropagateCarryBackward(buffer, this.position - 1);
            }

            this.position += readyByteCount;
            s = c + d - 24;
        }

        this.low = low << d;
        this.rng = rng << d;
        this.cnt = s;
    }

    /// <summary>
    /// Adds a carry to the completed output prefix.
    /// </summary>
    /// <param name="buffer">The accumulated output bytes.</param>
    /// <param name="offset">The final completed byte.</param>
    private static void PropagateCarryBackward(Span<byte> buffer, int offset)
    {
        int carry;
        do
        {
            int sum = buffer[offset] + 1;
            buffer[offset] = (byte)sum;
            carry = sum >> 8;
            offset--;
        }
        while (carry != 0);
    }
}
