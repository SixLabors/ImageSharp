// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Reads least-significant-bit-first JPEG XL fields from a caller-owned stream.
/// </summary>
/// <param name="stream">The stream bounded to the current codestream or section.</param>
/// <remarks>
/// Prefetch can advance the stream, but only reads and skips advance the logical bit position.
/// Peeks return zero for unavailable high bits so a Huffman lookup can select a shorter terminal code.
/// Reads and skips still require every consumed bit to exist in the input.
/// The caller owns the stream and must keep it alive while using this reader.
/// </remarks>
internal sealed class JxlBitReader(Stream stream)
{
    /// <summary>
    /// The next unread bits, starting at bit zero.
    /// </summary>
    private ulong buffer;

    /// <summary>
    /// The number of unread bits in the current buffer.
    /// </summary>
    private int bufferRemainingBits;

    /// <summary>
    /// Prefetched bits following the current buffer.
    /// </summary>
    private ulong lookahead;

    /// <summary>
    /// The number of valid bits in the lookahead buffer.
    /// </summary>
    private int lookaheadBits;

    /// <summary>
    /// The number of bytes fetched from the stream.
    /// </summary>
    private long bytesFetched;

    /// <summary>
    /// Whether the bounded input has ended. Repeated terminal Huffman peeks must not repeat stream reads.
    /// </summary>
    private bool endOfInput;

    /// <summary>
    /// Gets the number of bits consumed, excluding prefetched bits.
    /// </summary>
    public long TotalBitsConsumed => (this.bytesFetched * 8) - this.bufferRemainingBits - this.lookaheadBits;

    /// <summary>
    /// Reads up to one complete reservoir block, including streams that return short reads.
    /// </summary>
    /// <param name="value">The little-endian block, with unused high bits cleared.</param>
    /// <param name="bitCount">The number of valid bits in the block.</param>
    private void RefillCore(out ulong value, out int bitCount)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        int count = 0;

        while (count < bytes.Length)
        {
            int read = stream.Read(bytes[count..]);

            if (read == 0)
            {
                this.endOfInput = true;
                break;
            }

            count += read;
        }

        // Stream.Read may return fewer bytes than requested without reaching EOF. Only a zero
        // result ends the refill. Clear unused high bytes so no uninitialized stack data is read.
        bytes[count..].Clear();
        value = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        bitCount = count * 8;
        this.bytesFetched += count;
    }

    /// <summary>
    /// Buffers the available portion of a field without consuming bits or fabricating input length.
    /// </summary>
    /// <param name="bitCount">The field width, from one to 64 bits.</param>
    private void PrefetchBits(int bitCount)
    {
        if (this.bufferRemainingBits == 0 && !this.endOfInput)
        {
            this.RefillCore(out this.buffer, out this.bufferRemainingBits);
        }

        if (bitCount > this.bufferRemainingBits && this.lookaheadBits == 0 && !this.endOfInput)
        {
            this.RefillCore(out this.lookahead, out this.lookaheadBits);
        }
    }

    /// <summary>
    /// Reads or peeks one field using the same reservoir and bit ordering.
    /// </summary>
    /// <param name="bitCount">The field width, from zero to 64 bits.</param>
    /// <param name="peek">Whether to leave the logical position unchanged.</param>
    /// <returns>The unsigned field value.</returns>
    private ulong ReadBitsCore(int bitCount, bool peek)
    {
        if (bitCount == 0)
        {
            return 0;
        }

        this.PrefetchBits(bitCount);

        // Huffman table lookups can inspect more bits than their selected code consumes. Missing high bits remain zero,
        // as in the native reader. Only consumption must fit the actual buffered length; failure retains the available bits.
        if (!peek && bitCount > this.bufferRemainingBits + this.lookaheadBits)
        {
            throw new EndOfStreamException();
        }

        // Current bits occupy the low end. A field crossing the block boundary continues
        // into lookahead. Keep both blocks until consumption so repeated peeks are identical.
        ulong value = this.buffer;

        if (bitCount > this.bufferRemainingBits)
        {
            value |= this.lookahead << this.bufferRemainingBits;
        }

        // This mask also handles a full 64-bit field. Shifting 1UL by 64 would wrap to bit zero.
        value &= ulong.MaxValue >> (64 - bitCount);

        if (peek)
        {
            return value;
        }

        if (bitCount < this.bufferRemainingBits)
        {
            this.buffer >>= bitCount;
            this.bufferRemainingBits -= bitCount;
        }
        else
        {
            // Promote remaining lookahead bits after consuming the current block. The shift
            // is at most 63: a crossing field always consumes at least one current-buffer bit.
            int consumedLookahead = bitCount - this.bufferRemainingBits;
            this.buffer = this.lookahead >> consumedLookahead;
            this.bufferRemainingBits = this.lookaheadBits - consumedLookahead;
            this.lookahead = 0;
            this.lookaheadBits = 0;
        }

        return value;
    }

    /// <summary>
    /// Consumes zero padding up to the next byte boundary.
    /// </summary>
    /// <exception cref="InvalidImageContentException">The alignment bits are not zero.</exception>
    public void JumpToByteBoundary()
    {
        int padding = (int)((8 - (this.TotalBitsConsumed & 7)) & 7);

        if (this.ReadBits32((uint)padding) != 0)
        {
            throw new InvalidImageContentException("JPEG XL byte-alignment padding is not zero.");
        }
    }

    /// <summary>
    /// Reads an unsigned field of up to 32 bits.
    /// </summary>
    /// <param name="bitCount">The field width, from zero to 32 bits.</param>
    /// <returns>The field value.</returns>
    public uint ReadBits32(uint bitCount) => (uint)this.ReadBitsCore((int)bitCount, false);

    /// <summary>
    /// Peeks an unsigned field of up to 32 bits without consuming it. Unavailable high bits are zero.
    /// </summary>
    /// <param name="bitCount">The field width, from zero to 32 bits.</param>
    /// <returns>The field value.</returns>
    public uint PeekBits32(uint bitCount) => (uint)this.ReadBitsCore((int)bitCount, true);

    /// <summary>
    /// Skips the requested number of bits.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    public void SkipBits32(uint bitCount) => this.SkipBits64(bitCount);

    /// <summary>
    /// Reads an unsigned field of up to 64 bits.
    /// </summary>
    /// <param name="bitCount">The field width, from zero to 64 bits.</param>
    /// <returns>The field value.</returns>
    public ulong ReadBits64(ulong bitCount) => this.ReadBitsCore((int)bitCount, false);

    /// <summary>
    /// Peeks an unsigned field of up to 64 bits without consuming it. Unavailable high bits are zero.
    /// </summary>
    /// <param name="bitCount">The field width, from zero to 64 bits.</param>
    /// <returns>The field value.</returns>
    public ulong PeekBits64(ulong bitCount) => this.ReadBitsCore((int)bitCount, true);

    /// <summary>
    /// Skips a bit range, including ranges longer than one reservoir block.
    /// </summary>
    /// <param name="bitCount">The number of bits to skip.</param>
    public void SkipBits64(ulong bitCount)
    {
        while (bitCount >= 64)
        {
            this.ReadBitsCore(64, false);
            bitCount -= 64;
        }

        this.ReadBitsCore((int)bitCount, false);
    }

    /// <summary>
    /// Reads the next Boolean field.
    /// </summary>
    /// <returns>Whether the next bit is one.</returns>
    public bool ReadBoolean() => this.ReadBits32(1) != 0;
}
