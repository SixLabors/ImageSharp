// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Writes AV1 literals and adaptively coded symbols to a range-coded byte sequence.
/// </summary>
/// <remarks>
/// Each tile of a frame has its own tile buffer, which grows when it is full. The bytes of a tile stay in its buffer
/// until the same tile is written again, so the frame writer reads them from there and no frame buffer is necessary.
/// </remarks>
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
    /// The size of a new tile buffer in bytes. A tile that writes more bytes grows its buffer. As a result, this size only sets how
    /// often a large tile grows. It never limits the output.
    /// </summary>
    internal const int InitialTileBufferLength = 62025;

    /// <summary>
    /// The configuration that supplies output allocation.
    /// </summary>
    private readonly Configuration configuration;

    /// <summary>
    /// Indicates whether encoded symbols adapt their distributions.
    /// </summary>
    private readonly bool updateCdf;

    /// <summary>
    /// The owners of the tile buffers, by tile index. A tile that was not written yet has no buffer.
    /// </summary>
    private IMemoryOwner<byte>?[] tileOwners;

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
    /// The index of the tile that the range coder writes.
    /// </summary>
    private int tileIndex;

    /// <summary>
    /// The tile buffer of the current tile, with its current length.
    /// </summary>
    private Memory<byte> tileBuffer;

    /// <summary>
    /// The number of bytes of the current tile in the tile buffer.
    /// </summary>
    private int position;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1SymbolWriter"/> class.
    /// </summary>
    /// <param name="configuration">The configuration that supplies output allocation.</param>
    /// <param name="updateCdf">A value indicating whether encoded symbols adapt their distributions.</param>
    public Av1SymbolWriter(Configuration configuration, bool updateCdf)
    {
        this.configuration = configuration;
        this.updateCdf = updateCdf;
        IMemoryOwner<byte> owner = configuration.MemoryAllocator.Allocate<byte>(InitialTileBufferLength);
        this.tileOwners = [owner];
        this.tileBuffer = owner.Memory;
    }

    /// <summary>
    /// Restores the initial range-coder state and begins a new output sequence in the buffer of the first tile.
    /// </summary>
    public void Reset() => this.Reset(0);

    /// <summary>
    /// Restores the initial range-coder state for a tile, which the range coder then writes into the buffer of that tile.
    /// </summary>
    /// <param name="tileIndex">The index of the tile in the frame.</param>
    public void Reset(int tileIndex)
    {
        // A tile gets a buffer when it is written for the first time. The buffer then stays with the tile, at the
        // length it grew to, so later frames do not allocate it again.
        if (tileIndex >= this.tileOwners.Length)
        {
            Array.Resize(ref this.tileOwners, tileIndex + 1);
        }

        IMemoryOwner<byte> owner = this.tileOwners[tileIndex] ??= this.configuration.MemoryAllocator.Allocate<byte>(InitialTileBufferLength);
        this.tileIndex = tileIndex;
        this.tileBuffer = owner.Memory;
        this.low = 0;
        this.rng = InitialRange;
        this.cnt = InitialCount;
        this.position = 0;
    }

    /// <summary>
    /// Releases the tile buffers.
    /// </summary>
    public void Dispose()
    {
        foreach (IMemoryOwner<byte>? owner in this.tileOwners)
        {
            owner?.Dispose();
        }
    }

    /// <summary>
    /// Gets the tile buffer. The caller reads it once per tile and passes it to every write.
    /// </summary>
    /// <returns>The tile buffer.</returns>
    public Span<byte> GetTileBuffer() => this.tileBuffer.Span;

    /// <summary>
    /// Writes one binary symbol and adapts its distribution when CDF updates are enabled.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="symbol">The binary symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the binary alphabet.</param>
    public void WriteSymbol(ref Span<byte> output, bool symbol, Av1Distribution distribution)
        => this.WriteSymbol(ref output, symbol ? 1 : 0, distribution);

    /// <summary>
    /// Writes one symbol and adapts its distribution when CDF updates are enabled.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the symbol alphabet.</param>
    public void WriteSymbol(ref Span<byte> output, int symbol, Av1Distribution distribution)
    {
        DebugGuard.MustBeGreaterThanOrEqualTo(symbol, 0, nameof(symbol));
        DebugGuard.MustBeLessThan(symbol, distribution.NumberOfSymbols, nameof(symbol));
        DebugGuard.IsTrue(distribution[distribution.NumberOfSymbols - 1] == 0, "Last entry in Probabilities table needs to be zero.");

        this.EncodeIntegerQ15(ref output, symbol, distribution);
        this.UpdateSymbol(symbol, distribution);
    }

    /// <summary>
    /// Adapts a symbol distribution when probability updates are enabled, without emitting range-coded data.
    /// </summary>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="distribution">The inverse cumulative distribution for the symbol alphabet.</param>
    public void UpdateSymbol(int symbol, Av1Distribution distribution)
    {
        if (this.updateCdf)
        {
            distribution.Update(symbol);
        }
    }

    /// <summary>
    /// Writes one non-adaptive binary symbol using the supplied Q15 probability for <see langword="true"/>.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="value">The binary symbol.</param>
    /// <param name="frequency">The probability that the symbol is <see langword="true"/>, scaled by 32768.</param>
    public void WriteBoolean(ref Span<byte> output, bool value, uint frequency) => this.EncodeBoolQ15(ref output, value, frequency);

    /// <summary>
    /// Writes one equiprobable literal bit.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="value">The literal bit.</param>
    public void WriteLiteral(ref Span<byte> output, bool value) => this.WriteLiteral(ref output, value ? 1u : 0u, 1);

    /// <summary>
    /// Writes the requested low-order bits in most-significant-bit-first order.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="value">The unsigned literal value.</param>
    /// <param name="bitCount">The number of low-order bits to write.</param>
    public void WriteLiteral(ref Span<byte> output, uint value, int bitCount)
    {
        // A literal bit is equiprobable: its Q15 probability is half of 32768.
        const uint p = 0x4000U;
        for (int bit = bitCount - 1; bit >= 0; bit--)
        {
            bool bitValue = ((value >> bit) & 0x1) > 0;
            this.EncodeBoolQ15(ref output, bitValue, p);
        }
    }

    /// <summary>
    /// Terminates the range-coded sequence and copies it into a new owned byte buffer.
    /// </summary>
    /// <returns>An owner containing the shortest byte sequence that preserves every encoded symbol.</returns>
    public IMemoryOwner<byte> Exit()
    {
        int length = this.FinalizeRange();
        IMemoryOwner<byte> output = this.configuration.MemoryAllocator.Allocate<byte>(length);
        this.tileBuffer.Span[..length].CopyTo(output.Memory.Span);

        return output;
    }

    /// <summary>
    /// Terminates the range-coded sequence of the tile. Its bytes stay in the buffer of the tile.
    /// </summary>
    /// <returns>The number of encoded bytes of the tile.</returns>
    public int ExitTile() => this.FinalizeRange();

    /// <summary>
    /// Gets the encoded bytes of a tile from its tile buffer, without a copy.
    /// </summary>
    /// <param name="tileIndex">The index of the tile in the frame.</param>
    /// <param name="length">The number of encoded bytes that <see cref="ExitTile"/> returned for the tile.</param>
    /// <returns>The bytes of the tile, valid until the tile is written again or this writer is disposed.</returns>
    public ReadOnlySpan<byte> GetTileOutput(int tileIndex, int length) => this.tileOwners[tileIndex]!.Memory.Span[..length];

    /// <summary>
    /// Terminates the range-coded sequence in the tile buffer.
    /// </summary>
    /// <returns>The number of encoded bytes in the tile buffer.</returns>
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

        // The terminating bytes can exceed the tile buffer. It then grows to the exact length that they need.
        int pendingByteCount = Math.Max((s + 7) >> 3, 0);
        Span<byte> buffer = this.tileBuffer.Span;
        if (pos + pendingByteCount > buffer.Length)
        {
            buffer = this.GrowTileBuffer(pos + pendingByteCount);
        }

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
    /// Encodes one binary value with a fixed Q15 probability.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="val">The value to encode.</param>
    /// <param name="frequency">The probability that the value is true, scaled by 32768.</param>
    private void EncodeBoolQ15(ref Span<byte> output, bool val, uint frequency)
    {
        ulong l;
        uint r;
        uint v;
        DebugGuard.MustBeGreaterThan(frequency, 0U, nameof(frequency));
        DebugGuard.MustBeLessThan(frequency, 32768U, nameof(frequency));
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

        this.Normalize(ref output, l, r);
    }

    /// <summary>
    /// Encodes a symbol with an inverse cumulative distribution function (CDF) table in Q15.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="symbol">The value to encode.</param>
    /// <param name="distribution">
    /// The inverse CDF: <see cref="Av1Distribution.ProbabilityTop"/> minus the CDF. Symbol s covers the range from
    /// (s &gt; 0 ? ProbabilityTop - icdf[s - 1] : 0) to ProbabilityTop - icdf[s], with the upper bound excluded.
    /// The values must not increase, and the last value must be 0.
    /// </param>
    private void EncodeIntegerQ15(ref Span<byte> output, int symbol, Av1Distribution distribution)
        => this.EncodeIntegerQ15(
            ref output,
            symbol > 0 ? distribution[symbol - 1] : Av1Distribution.ProbabilityTop,
            distribution[symbol],
            symbol,
            distribution.NumberOfSymbols);

    /// <summary>
    /// Narrows the coding interval to one symbol's inverse-cumulative bounds.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A write that grows the buffer replaces it.</param>
    /// <param name="lowFrequency">The inverse cumulative threshold preceding the symbol.</param>
    /// <param name="highFrequency">The inverse cumulative threshold following the symbol.</param>
    /// <param name="symbol">The zero-based symbol.</param>
    /// <param name="numberOfSymbols">The size of the symbol alphabet.</param>
    private void EncodeIntegerQ15(ref Span<byte> output, uint lowFrequency, uint highFrequency, int symbol, int numberOfSymbols)
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

        this.Normalize(ref output, l, r);
    }

    /// <summary>
    /// Renormalizes updated low and range values so that <paramref name="rng"/> lies between 32768 and 65536, and stores them in the
    /// encoder state. When enough bits are complete, it flushes bytes from low to the tile buffer.
    /// </summary>
    /// <param name="output">The tile buffer from <see cref="GetTileBuffer"/>. A flush that grows the buffer replaces it.</param>
    /// <param name="low">The new value of <see cref="low"/>.</param>
    /// <param name="rng">The new value of <see cref="rng"/>.</param>
    private void Normalize(ref Span<byte> output, ulong low, uint rng)
    {
        // A write must use the current tile buffer. An old span from before a growth points to released memory.
        DebugGuard.IsTrue(output.Length == this.tileBuffer.Length, nameof(output), "The output must be the current tile buffer.");
        int c = this.cnt;
        DebugGuard.MustBeLessThanOrEqualTo(rng, 65535U, nameof(rng));
        int d = 15 - Av1Math.MostSignificantBit(rng);
        int s = c + d;

        // Keeping 16 bits free for the next symbol allows the 64-bit coding window to flush up to eight completed
        // bytes together while preserving one carry bit.
        if (s >= 40)
        {
            // A word store touches eight bytes even when fewer become logical output. When the tile buffer has no
            // room for it, the buffer grows to twice its length plus one word, and the caller gets the new buffer.
            if (this.position + sizeof(ulong) > output.Length)
            {
                output = this.GrowTileBuffer(checked((2 * this.tileBuffer.Length) + sizeof(ulong)));
            }

            int readyByteCount = (s >> 3) + 1;
            c += 24 - (readyByteCount << 3);
            ulong bytes = low >> c;
            low &= (1UL << c) - 1;
            ulong carryMask = 1UL << (readyByteCount << 3);
            bool hasCarry = (bytes & carryMask) != 0;
            bytes &= carryMask - 1;

            // One big-endian word store avoids a byte-at-a-time hot loop. Only readyByteCount bytes become part of the logical
            // output. The next flush overwrites the bytes after them.
            BinaryPrimitives.WriteUInt64BigEndian(
                output.Slice(this.position, sizeof(ulong)),
                bytes << ((sizeof(ulong) - readyByteCount) << 3));

            if (hasCarry)
            {
                PropagateCarryBackward(output, this.position - 1);
            }

            this.position += readyByteCount;
            s = c + d - 24;
        }

        this.low = low << d;
        this.rng = rng << d;
        this.cnt = s;
    }

    /// <summary>
    /// Replaces the tile buffer with a larger one and keeps the bytes of the current tile.
    /// </summary>
    /// <param name="length">The length of the new tile buffer in bytes.</param>
    /// <returns>The new tile buffer.</returns>
    private Span<byte> GrowTileBuffer(int length)
    {
        IMemoryOwner<byte> replacement = this.configuration.MemoryAllocator.Allocate<byte>(length);
        Memory<byte> replacementBuffer = replacement.Memory;

        // Only the completed bytes of the current tile move. Pending bits stay in low and cnt.
        this.tileBuffer.Span[..this.position].CopyTo(replacementBuffer.Span);
        IMemoryOwner<byte> previous = this.tileOwners[this.tileIndex]!;
        this.tileOwners[this.tileIndex] = replacement;
        previous.Dispose();
        this.tileBuffer = replacementBuffer;
        return replacementBuffer.Span;
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
