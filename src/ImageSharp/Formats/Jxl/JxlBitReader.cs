// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Reads least-significant-bit-first JPEG XL header fields from a bounded codestream.
/// </summary>
/// <remarks>
/// The parser owns this mutable value. Pass it by reference between header parsing methods so cached bits are not copied.
/// The codestream reader owns fragment traversal and borrows the decoder's buffered input.
/// </remarks>
internal struct JxlBitReader
{
    private static readonly UTF8Encoding NameEncoding = new(false, true);

    private readonly JxlCodestreamReader source;
    private ulong buffer;
    private int availableBits;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBitReader"/> struct.
    /// </summary>
    /// <param name="source">The logical codestream, positioned at the first field to read.</param>
    public JxlBitReader(JxlCodestreamReader source)
    {
        this.source = source;
        this.buffer = 0;
        this.availableBits = 0;
    }

    /// <summary>
    /// Gets the number of logical codestream bits consumed, excluding prefetched bits.
    /// </summary>
    public readonly long Position => (this.source.Position * 8) - this.availableBits;

    /// <summary>
    /// Gets the number of complete logical codestream bytes consumed, excluding prefetched bits.
    /// </summary>
    public readonly long BytePosition => this.source.Position - ((this.availableBits + 7) / 8);

    /// <summary>
    /// Rejects a bit count that cannot fit in the remaining input, including prefetched bits.
    /// Container headers can make this upper bound larger than the actual remaining codestream.
    /// </summary>
    /// <param name="count">The minimum bit count required by the consuming syntax.</param>
    public readonly void CheckBitBudget(ulong count)
    {
        // Count cached bits first, including an incomplete byte. Round only the uncached part up to whole bytes.
        // Divide before rounding so neither an arbitrary file length nor a declared bit count is multiplied by eight.
        ulong uncached = count - Math.Min(count, (ulong)this.availableBits);
        ulong requiredBytes = (uncached >> 3) + ((uncached & 7) != 0 ? 1UL : 0UL);
        if (requiredBytes > (ulong)(this.source.MaximumLength - this.source.Position))
        {
            throw new InvalidImageContentException("The JPEG XL field cannot fit in the remaining codestream.");
        }
    }

    /// <summary>
    /// Rejects a byte count that cannot fit in the remaining input, including complete prefetched bytes.
    /// Container headers can make this upper bound larger than the actual remaining codestream.
    /// </summary>
    /// <param name="count">The nonnegative payload byte count declared by the consuming syntax.</param>
    public readonly void CheckByteBudget(long count)
    {
        // Complete cached bytes contribute directly. Any remaining fraction stays available, but cannot supply another whole byte.
        // Subtract before comparing so large byte counts never need a potentially overflowing conversion to bits.
        long uncached = count - Math.Min(count, this.availableBits / 8);
        if (uncached > this.source.MaximumLength - this.source.Position)
        {
            throw new InvalidImageContentException("The JPEG XL payload cannot fit in the remaining codestream.");
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
        uint result = this.PeekBits(count);
        this.ConsumeBits(count);
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

        // The parser supplies widths in [0,32]. A 64-bit mask keeps both endpoint widths valid: a 32-bit shift
        // would wrap at 32 and return an empty mask. Cached bytes remain above the consumed low bits.
        return (uint)(this.buffer & ((1UL << count) - 1));
    }

    /// <summary>
    /// Consumes bits already exposed by lookahead and rejects consumption of its zero padding.
    /// </summary>
    /// <param name="count">The encoded field or prefix-code length.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConsumeBits(int count)
    {
        if (count > this.availableBits)
        {
            throw new InvalidImageContentException("The JPEG XL field exceeds its codestream boundary.");
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
        // The syntax choices bound both the width and sum; no untrusted value selects a new width or offset here.
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
    public void ReadBytes(Span<byte> destination)
    {
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

        // After draining the prefetched whole bytes, copy aligned input through the existing buffered reader.
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
        int cached = (int)Math.Min(count, (ulong)this.availableBits);
        this.buffer = cached == 64 ? 0 : this.buffer >> cached;
        this.availableBits -= cached;
        count -= (uint)cached;

        if (count != 0)
        {
            // Once the reservoir is empty, the physical input is byte-aligned. Seek whole bytes instead of
            // issuing one buffered read for every machine word of an unknown extension.
            this.source.Skip((long)(count >> 3));
            this.ReadBits((int)(count & 7));
        }
    }

    /// <summary>
    /// Skips a nonnegative byte count from a byte-aligned field without converting the count to bits.
    /// </summary>
    /// <param name="count">The payload length declared by the consuming syntax.</param>
    public void SkipBytes(long count)
    {
        // Table payloads begin at a byte boundary, but lookahead can retain whole bytes from that payload.
        // Drain those bytes before seeking the source. A 64-bit shift needs an explicit zero result because shifts wrap at 64.
        int cached = (int)Math.Min(count, this.availableBits / 8);
        int bits = cached * 8;
        this.buffer = bits == 64 ? 0 : this.buffer >> bits;
        this.availableBits -= bits;
        this.source.Skip(count - cached);
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
    /// <returns>The decoded label, or an empty string when the field is empty.</returns>
    public string ReadName()
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
            throw new InvalidImageContentException("The JPEG XL name is not valid UTF-8.", exception);
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
    /// Refills the 64-bit reservoir with whole bytes until the next field is available.
    /// </summary>
    /// <param name="count">The number of bits needed by the pending field.</param>
    private void Refill(int count)
    {
        while (this.availableBits < count)
        {
            InlineArray8<byte> storage = default;
            Span<byte> bytes = storage;
            int capacity = (64 - this.availableBits) / 8;
            int read = this.source.Read(bytes[..capacity]);
            if (read == 0)
            {
                // Prefix lookup may inspect zero padding. ConsumeBits still requires every consumed bit to
                // exist in the input, including when the selected code is shorter than the lookup table width.
                break;
            }

            // A container read can end at a fragment boundary. Unwritten bytes remain zero and do not become
            // available bits. The next iteration appends more bytes at the existing bit count, including unaligned fields.
            this.buffer |= BinaryPrimitives.ReadUInt64LittleEndian(bytes) << this.availableBits;
            this.availableBits += read * 8;
        }
    }
}
