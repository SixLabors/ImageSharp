// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Reads least-significant-bit-first JPEG XL header fields from a bounded codestream.
/// </summary>
/// <remarks>
/// The parser owns this mutable value. Pass it by reference between header parsing methods so cached bits are not copied.
/// The codestream reader owns fragment traversal and borrows the decoder's buffered input.
/// The caller owns the read window and must keep it allocated until parsing finishes.
/// </remarks>
internal ref struct JxlBitReader
{
    private static readonly UTF8Encoding NameEncoding = new(false, true);

    private readonly JxlCodestreamReader source;
    private readonly Span<byte> readBuffer;
    private ReadOnlySpan<byte> unreadBytes;
    private ulong buffer;
    private int availableBits;

    /// <summary>
    /// The exclusive logical bit position of the current section. The maximum value disables the additional section bound.
    /// </summary>
    private long sectionEnd;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBitReader"/> struct.
    /// </summary>
    /// <param name="source">The logical codestream, positioned at the first field to read.</param>
    /// <param name="readBuffer">Caller-owned read storage of at least eight bytes, retained for this reader's lifetime.</param>
    public JxlBitReader(JxlCodestreamReader source, Span<byte> readBuffer)
    {
        this.source = source;
        this.readBuffer = readBuffer;
        this.unreadBytes = [];
        this.buffer = 0;
        this.availableBits = 0;
        this.sectionEnd = long.MaxValue;
    }

    /// <summary>
    /// Gets the number of logical codestream bits consumed, excluding prefetched bits.
    /// </summary>
    public readonly long Position => ((this.source.Position - this.unreadBytes.Length) * 8) - this.availableBits;

    /// <summary>
    /// Gets the number of complete logical codestream bytes consumed, excluding prefetched bits.
    /// </summary>
    public readonly long BytePosition => this.source.Position - this.unreadBytes.Length - ((this.availableBits + 7) / 8);

    /// <summary>
    /// Positions the codestream at a byte offset and discards lookahead and the preceding section bound.
    /// </summary>
    /// <param name="position">The logical byte offset selected from the frame's validated section table.</param>
    public void SeekBytePosition(long position)
    {
        // A section can move backward or across container fragments. Its new bytes must come from
        // the source's selected range, not bits prefetched at the preceding section's physical position.
        this.source.Seek(position);
        this.unreadBytes = [];
        this.buffer = 0;
        this.availableBits = 0;
        this.sectionEnd = long.MaxValue;
    }

    /// <summary>
    /// Bounds subsequent reads to one byte-aligned frame section without copying its payload.
    /// </summary>
    /// <param name="byteLength">The section length from the frame's validated section table.</param>
    public void BeginSection(uint byteLength)
    {
        // The section table ends on a byte boundary. Header lookahead can already contain payload
        // or following-section bytes, so retain the reservoir and bound logical consumption instead.
        this.sectionEnd = this.Position + ((long)byteLength * 8);
    }

    /// <summary>
    /// Skips the unused part of the current section and restores unrestricted codestream reads.
    /// </summary>
    /// <remarks>The caller must first select a section with <see cref="BeginSection"/>.</remarks>
    /// <exception cref="InvalidImageContentException">The declared section extends past the available codestream.</exception>
    public void EndSection()
    {
        // Native section readers permit unused bytes and do not validate their padding values.
        // Skip only the declared remainder. Cached bytes after that boundary remain available to the next section.
        this.SkipBits((ulong)(this.sectionEnd - this.Position));
        this.sectionEnd = long.MaxValue;
    }

    /// <summary>
    /// Rejects a bit count that cannot fit in the remaining input, including prefetched bits.
    /// Container headers can make this upper bound larger than the actual remaining codestream.
    /// </summary>
    /// <param name="count">The minimum bit count required by the consuming syntax.</param>
    public readonly void CheckBitBudget(ulong count)
    {
        if (count > (ulong)(this.sectionEnd - this.Position))
        {
            throw new InvalidImageContentException("The JPEG XL field cannot fit in the remaining section.");
        }

        // Count both read caches first. The source position already includes every prefetched window byte.
        // Round only uncached bits up to whole bytes, without multiplying the physical file length by eight.
        ulong cached = (ulong)this.availableBits + ((ulong)this.unreadBytes.Length * 8);
        ulong uncached = count - Math.Min(count, cached);
        ulong requiredBytes = (uncached >> 3) + ((uncached & 7) != 0 ? 1UL : 0UL);
        if (requiredBytes > (ulong)(this.source.MaximumLength - this.source.Position))
        {
            throw new InvalidImageContentException("The JPEG XL field cannot fit in the remaining codestream.");
        }
    }

    /// <summary>
    /// Reads an unsigned field of zero to 32 bits. The first encoded bit becomes the least significant result bit.
    /// </summary>
    /// <param name="count">The field width selected by the codec syntax.</param>
    /// <returns>The unsigned field value.</returns>
    /// <exception cref="InvalidImageContentException">The field exceeds the available codestream.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint ReadBits(int count)
    {
        if (this.availableBits < count)
        {
            this.Refill(count);
        }

        // A consuming read needs no zero-padded lookahead. Validate once before extraction instead of
        // recalculating the logical section position in both PeekBits and ConsumeBits for the same field.
        if (count > this.availableBits || count > this.sectionEnd - this.Position)
        {
            throw new InvalidImageContentException("The JPEG XL field exceeds its section or codestream boundary.");
        }

        uint result = (uint)(this.buffer & ((1UL << count) - 1));
        this.buffer >>= count;
        this.availableBits -= count;
        return result;
    }

    /// <summary>
    /// Looks ahead by up to 32 bits without consuming them. Unavailable high bits are zero for prefix-table lookup.
    /// </summary>
    /// <param name="count">The lookup width selected by the codec.</param>
    /// <returns>The next bits, with zero padding only for lookahead.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint PeekBits(int count)
    {
        if (this.availableBits < count)
        {
            this.Refill(count);
        }

        // A lookup can inspect zero padding but must not inspect prefetched bits from the following section.
        // Mask only the requested bits inside the bound. A 64-bit mask also preserves the zero- and 32-bit cases.
        int readable = (int)Math.Min(count, this.sectionEnd - this.Position);
        return (uint)(this.buffer & ((1UL << readable) - 1));
    }

    /// <summary>
    /// Consumes bits already exposed by lookahead and rejects consumption of its zero padding.
    /// </summary>
    /// <param name="count">The encoded field or prefix-code length.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConsumeBits(int count)
    {
        if (count > this.availableBits || count > this.sectionEnd - this.Position)
        {
            throw new InvalidImageContentException("The JPEG XL field exceeds its section or codestream boundary.");
        }

        this.buffer >>= count;
        this.availableBits -= count;
    }

    /// <summary>
    /// Reads one Boolean field.
    /// </summary>
    /// <returns>Whether the encoded bit is one.</returns>
    public bool ReadBoolean() => this.ReadBits(1) != 0;

    /// <summary>
    /// Reads the two-bit selector and selected unsigned field used by JPEG XL's U32 coding.
    /// </summary>
    /// <param name="first">The bit width and offset selected by zero.</param>
    /// <param name="second">The bit width and offset selected by one.</param>
    /// <param name="third">The bit width and offset selected by two.</param>
    /// <param name="fourth">The bit width and offset selected by three.</param>
    /// <returns>The selected offset plus the unsigned field value.</returns>
    public uint ReadU32((int Bits, uint Offset) first, (int Bits, uint Offset) second, (int Bits, uint Offset) third, (int Bits, uint Offset) fourth)
    {
        // Choices are fixed by each header field. A zero-width choice encodes its offset without a payload.
        // The caller fixes every width and offset. Addition uses unsigned 32-bit arithmetic, including
        // the wrap specified for signed DC thresholds whose last choice carries a full 32-bit payload.
        (int bits, uint offset) = this.ReadBits(2) switch
        {
            0 => first,
            1 => second,
            2 => third,
            _ => fourth
        };

        return offset + this.ReadBits(bits);
    }

    /// <summary>
    /// Reads a JPEG XL unsigned 64-bit field, including its bounded continuation groups.
    /// </summary>
    /// <returns>The unsigned value.</returns>
    public ulong ReadU64()
    {
        uint selector = this.ReadBits(2);
        if (selector == 0)
        {
            return 0;
        }

        if (selector == 1)
        {
            return 1 + this.ReadBits(4);
        }

        if (selector == 2)
        {
            return 17 + this.ReadBits(8);
        }

        ulong value = this.ReadBits(12);
        for (int shift = 12; this.ReadBoolean(); shift += 8)
        {
            // Six eight-bit groups follow the initial twelve bits. The final group has only four bits;
            // it reaches bit 63 without overflowing the accumulator or requiring a terminator beyond the field.
            int width = shift == 60 ? 4 : 8;
            value |= (ulong)this.ReadBits(width) << shift;
            if (shift == 60)
            {
                break;
            }
        }

        return value;
    }

    /// <summary>
    /// Reads a JPEG XL enumeration and validates it against the format's allowed-value mask.
    /// </summary>
    /// <param name="allowedValues">A bit for each allowed value in the range zero to 63.</param>
    /// <returns>The encoded enumeration value.</returns>
    public uint ReadEnum(ulong allowedValues)
    {
        uint value = this.ReadU32((0, 0), (0, 1), (4, 2), (6, 18));
        if (value >= 64 || (allowedValues & (1UL << (int)value)) == 0)
        {
            throw new InvalidImageContentException("The JPEG XL enumeration value is not supported by the format.");
        }

        return value;
    }

    /// <summary>
    /// Reads a finite half-precision header value through the shared half-storage conversion.
    /// </summary>
    /// <returns>The exact single-precision expansion of the encoded value.</returns>
    public float ReadHalf()
    {
        float value = HalfTypeHelper.Unpack((ushort)this.ReadBits(16));
        if (!float.IsFinite(value))
        {
            // Pixel formats can store IEEE special values, but JPEG XL's F16 header syntax forbids them.
            // Reject invalid syntax here; do not change the shared half conversion or clamp the encoded value.
            throw new InvalidImageContentException("JPEG XL half-precision header fields cannot contain NaN or infinity.");
        }

        return value;
    }

    /// <summary>
    /// Reads successive byte fields, retaining the current bit alignment.
    /// </summary>
    /// <param name="destination">The span receiving the encoded byte fields.</param>
    public void ReadBytes(scoped Span<byte> destination)
    {
        // The aligned copy below bypasses ReadBits. Prove that its complete byte field stays inside
        // the current section before either draining lookahead or reading directly from the codestream.
        this.CheckBitBudget((ulong)destination.Length * 8);

        if ((this.availableBits & 7) != 0)
        {
            // Names and other byte-valued fields can begin between physical bytes. Their bits must pass through
            // the reservoir; copying physical bytes directly would shift every character.
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = (byte)this.ReadBits(8);
            }

            return;
        }

        int written = 0;
        while (this.availableBits != 0 && written < destination.Length)
        {
            destination[written++] = (byte)this.ReadBits(8);
        }

        // The read window follows the reservoir in logical order. Drain it before reading physical bytes,
        // because the source position already follows this prefetched range.
        int cached = Math.Min(this.unreadBytes.Length, destination.Length - written);
        this.unreadBytes[..cached].CopyTo(destination[written..]);
        this.unreadBytes = this.unreadBytes[cached..];
        written += cached;

        // A physical fragment can end before this byte field does, so continue until the destination is full.
        while (written < destination.Length)
        {
            int read = this.source.Read(destination[written..]);
            if (read == 0)
            {
                throw new InvalidImageContentException("The JPEG XL byte field exceeds its codestream boundary.");
            }

            written += read;
        }
    }

    /// <summary>
    /// Skips an unsigned bit count while preserving prefetched data and container boundaries.
    /// </summary>
    /// <param name="count">The bit count declared by the header.</param>
    public void SkipBits(ulong count)
    {
        if (count > (ulong)(this.sectionEnd - this.Position))
        {
            throw new InvalidImageContentException("The JPEG XL field exceeds its section boundary.");
        }

        int cached = (int)Math.Min(count, (ulong)this.availableBits);
        this.buffer = cached == 64 ? 0 : this.buffer >> cached;
        this.availableBits -= cached;
        count -= (uint)cached;

        if (count != 0)
        {
            // The reservoir is empty and its next byte comes from the borrowed read window. Skip that
            // range before seeking physical bytes, or prefetched bytes would be counted twice.
            int cachedBytes = (int)Math.Min(count >> 3, (ulong)this.unreadBytes.Length);
            this.unreadBytes = this.unreadBytes[cachedBytes..];
            count -= (uint)cachedBytes * 8UL;
            if (!this.source.TrySkip((long)(count >> 3)))
            {
                throw new InvalidImageContentException("The JPEG XL field exceeds its codestream boundary.");
            }

            this.ReadBits((int)(count & 7));
        }
    }

    /// <summary>
    /// Skips a nonnegative byte count from a byte-aligned payload without converting the count to bits.
    /// </summary>
    /// <param name="count">The payload length declared by the consuming syntax.</param>
    /// <returns>Whether the complete payload was available within the current section and codestream. Container errors still throw.</returns>
    public bool TrySkipBytes(long count)
    {
        // Match a short codestream's behavior at a section boundary: consume the available range and
        // report false. The caller can recover without advancing into the following section's payload.
        long boundedCount = Math.Min(count, (this.sectionEnd - this.Position) / 8);

        // Table payloads begin at a byte boundary, but lookahead can retain whole bytes from that payload.
        // Drain those bytes before seeking the source. A 64-bit shift needs an explicit zero result because shifts wrap at 64.
        int cached = (int)Math.Min(boundedCount, this.availableBits / 8);
        int bits = cached * 8;
        this.buffer = bits == 64 ? 0 : this.buffer >> bits;
        this.availableBits -= bits;
        long remaining = boundedCount - cached;
        int cachedBytes = (int)Math.Min(remaining, this.unreadBytes.Length);
        this.unreadBytes = this.unreadBytes[cachedBytes..];
        bool complete = this.source.TrySkip(remaining - cachedBytes);
        return complete && boundedCount == count;
    }

    /// <summary>
    /// Consumes zero padding through the next byte boundary.
    /// </summary>
    public void AlignToByte()
    {
        if (this.ReadBits(this.availableBits & 7) != 0)
        {
            throw new InvalidImageContentException("The JPEG XL byte-alignment padding is not zero.");
        }
    }

    /// <summary>
    /// Reads a bounded UTF-8 frame or extra-channel name.
    /// </summary>
    /// <param name="handling">The policy for an invalid optional label. Missing field bytes always remain fatal.</param>
    /// <returns>The decoded label, or an empty string for an empty or ignored invalid field.</returns>
    public string ReadName(SegmentIntegrityHandling handling)
    {
        int length = (int)this.ReadU32((0, 0), (4, 0), (5, 16), (10, 48));
        Span<byte> storage = stackalloc byte[JxlConstants.MaximumNameByteLength];
        this.ReadBytes(storage[..length]);
        try
        {
            // The syntax bounds encoded bytes, rather than UTF-16 characters. Decode directly from stack storage.
            return NameEncoding.GetString(storage[..length]);
        }
        catch (DecoderFallbackException exception)
        {
            if (handling == SegmentIntegrityHandling.Strict)
            {
                throw new InvalidImageContentException("The JPEG XL name is not valid UTF-8.", exception);
            }

            // The complete byte field has already been consumed. Its text cannot affect reconstruction,
            // so both non-strict policies can omit the label without losing the next field's bit position.
            return string.Empty;
        }
    }

    /// <summary>
    /// Skips an extension group after validating its declared bit lengths.
    /// </summary>
    public void SkipExtensions()
    {
        ulong extensions = this.ReadU64();
        ulong bits = 0;
        while (extensions != 0)
        {
            ulong length = this.ReadU64();
            if (length > ulong.MaxValue - bits)
            {
                throw new InvalidImageContentException("The JPEG XL extension lengths overflow.");
            }

            bits += length;
            extensions &= extensions - 1;
        }

        // All lengths precede all payloads. Skip the combined range without materializing unknown extensions.
        this.SkipBits(bits);
    }

    /// <summary>
    /// Reads an image size header, also used for the image's optional intrinsic size.
    /// </summary>
    /// <returns>The encoded width and height.</returns>
    /// <exception cref="InvalidImageContentException">The width exceeds the supported image dimension range.</exception>
    public Size ReadSize()
    {
        bool small = this.ReadBoolean();
        uint height = small ? (this.ReadBits(5) + 1) * 8 : this.ReadU32((9, 1), (13, 1), (18, 1), (30, 1));
        uint ratio = this.ReadBits(3);
        ulong width;
        if (ratio == 0)
        {
            width = small ? (this.ReadBits(5) + 1) * 8 : this.ReadU32((9, 1), (13, 1), (18, 1), (30, 1));
        }
        else
        {
            // Nonzero ratio codes omit the width. The format requires integer truncation after applying one of
            // these fixed width/height ratios. Widen before multiplying so large dimensions do not wrap.
            width = ratio switch
            {
                1 => height,
                2 => height * 12UL / 10,
                3 => height * 4UL / 3,
                4 => height * 3UL / 2,
                5 => height * 16UL / 9,
                6 => height * 5UL / 4,
                _ => height * 2UL
            };
        }

        // The height and an explicit width are at most 2^30. Only a width derived from the aspect ratio can
        // exceed ImageSharp's signed dimension range, so validate that external value before narrowing it.
        if (width > int.MaxValue)
        {
            throw new InvalidImageContentException("The JPEG XL image width exceeds the supported dimension range.");
        }

        return new Size((int)width, (int)height);
    }

    /// <summary>
    /// Refills the reservoir from the borrowed read window, preserving exact section and fragment boundaries.
    /// </summary>
    /// <param name="count">The number of bits needed by the pending field.</param>
    private void Refill(int count)
    {
        while (this.availableBits < count)
        {
            // Logical consumption excludes both caches. The next reservoir byte follows only the cached bits;
            // it can still be in the window even when the physical source has passed the section end.
            long nextByte = this.source.Position - this.unreadBytes.Length;
            long remainingBytes = (this.sectionEnd / 8) - nextByte;
            if (remainingBytes <= 0)
            {
                break;
            }

            if (this.unreadBytes.IsEmpty)
            {
                // Read one bounded physical range. CodestreamReader validates container sequencing and can
                // return at a fragment boundary. No stream call, copy or fragment lookup occurs per machine word.
                int capacity = (int)Math.Min(this.readBuffer.Length, remainingBytes);
                int read = this.source.Read(this.readBuffer[..capacity]);
                if (read == 0)
                {
                    // Prefix lookup can inspect zero padding, but ConsumeBits cannot consume missing input.
                    break;
                }

                this.unreadBytes = this.readBuffer[..read];
            }

            int bytesToConsume = (int)Math.Min(Math.Min((64 - this.availableBits) / 8, this.unreadBytes.Length), remainingBytes);
            ulong next;
            if (this.unreadBytes.Length >= sizeof(ulong))
            {
                // The unaligned load stays inside caller-owned storage. Only the consumed low bytes enter
                // the reservoir; bytes after a section boundary or an eventual seek cannot leave stale high bits.
                next = BinaryPrimitives.ReadUInt64LittleEndian(this.unreadBytes);
                if (bytesToConsume < sizeof(ulong))
                {
                    next &= (1UL << (bytesToConsume * 8)) - 1;
                }
            }
            else
            {
                // A short window tail can precede another fragment. Zero only this final machine word and
                // append the exact available count, so padding never becomes consumable encoded data.
                InlineArray8<byte> storage = default;
                Span<byte> tail = storage;
                this.unreadBytes[..bytesToConsume].CopyTo(tail);
                next = BinaryPrimitives.ReadUInt64LittleEndian(tail);
            }

            this.buffer |= next << this.availableBits;
            this.availableBits += bytesToConsume * 8;
            this.unreadBytes = this.unreadBytes[bytesToConsume..];
        }
    }
}
