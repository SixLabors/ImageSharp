// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Reads fixed-width values and null-terminated strings from one bounded HEIF box payload.
/// </summary>
internal ref struct HeifBoxPayloadReader
{
    /// <summary>
    /// The source stream shared by the container parser.
    /// </summary>
    private readonly Stream stream;

    /// <summary>
    /// The caller-owned buffer reused for sequential values.
    /// </summary>
    private readonly Span<byte> buffer;

    /// <summary>
    /// The payload name used in malformed-image diagnostics.
    /// </summary>
    private readonly string name;

    /// <summary>
    /// The number of bytes not yet loaded from the bounded payload.
    /// </summary>
    private long remaining;

    /// <summary>
    /// The next unread byte in <see cref="buffer"/>.
    /// </summary>
    private int offset;

    /// <summary>
    /// The number of valid bytes currently stored in <see cref="buffer"/>.
    /// </summary>
    private int count;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifBoxPayloadReader"/> struct.
    /// </summary>
    /// <param name="stream">The stream positioned at the bounded payload.</param>
    /// <param name="length">The exact number of payload bytes.</param>
    /// <param name="buffer">The caller-owned reusable buffer.</param>
    /// <param name="name">The payload name used in malformed-image diagnostics.</param>
    public HeifBoxPayloadReader(Stream stream, long length, Span<byte> buffer, string name)
    {
        this.stream = stream;
        this.buffer = buffer;
        this.name = name;
        this.remaining = length;
        this.offset = 0;
        this.count = 0;
    }

    /// <summary>
    /// Gets the number of unread bytes in the bounded payload.
    /// </summary>
    public readonly long Remaining => this.remaining + this.count - this.offset;

    /// <summary>
    /// Gets a value indicating whether the complete bounded payload has been consumed.
    /// </summary>
    public readonly bool IsComplete => this.Remaining == 0;

    /// <summary>
    /// Reads one unsigned byte from the bounded payload.
    /// </summary>
    /// <returns>The next byte.</returns>
    public byte ReadByte()
    {
        this.Ensure(1);
        return this.buffer[this.offset++];
    }

    /// <summary>
    /// Reads one big-endian unsigned 16-bit value from the bounded payload.
    /// </summary>
    /// <returns>The next 16-bit value.</returns>
    public ushort ReadUInt16()
    {
        this.Ensure(2);
        ushort value = BinaryPrimitives.ReadUInt16BigEndian(this.buffer[this.offset..]);
        this.offset += 2;
        return value;
    }

    /// <summary>
    /// Reads one big-endian unsigned 32-bit value from the bounded payload.
    /// </summary>
    /// <returns>The next 32-bit value.</returns>
    public uint ReadUInt32()
    {
        this.Ensure(4);
        uint value = BinaryPrimitives.ReadUInt32BigEndian(this.buffer[this.offset..]);
        this.offset += 4;
        return value;
    }

    /// <summary>
    /// Reads one big-endian unsigned 64-bit value from the bounded payload.
    /// </summary>
    /// <returns>The next 64-bit value.</returns>
    public ulong ReadUInt64()
    {
        this.Ensure(8);
        ulong value = BinaryPrimitives.ReadUInt64BigEndian(this.buffer[this.offset..]);
        this.offset += 8;
        return value;
    }

    /// <summary>
    /// Reads a zero-width, 32-bit, or 64-bit unsigned field.
    /// </summary>
    /// <param name="size">The field width in bytes.</param>
    /// <returns>The decoded unsigned value.</returns>
    public ulong ReadVariableUInt(int size) => size switch
    {
        0 => 0,
        4 => this.ReadUInt32(),
        8 => this.ReadUInt64(),
        _ => throw new InvalidImageContentException($"The {this.name} payload uses an unsupported integer field size.")
    };

    /// <summary>
    /// Consumes one null-terminated byte string without materializing it.
    /// </summary>
    public void SkipNullTerminatedString()
    {
        while (this.Remaining > 0)
        {
            if (this.ReadByte() == 0)
            {
                return;
            }
        }

        throw new InvalidImageContentException($"The {this.name} payload contains an unterminated string.");
    }

    /// <summary>
    /// Consumes one null-terminated byte string and compares it with an expected ASCII value.
    /// </summary>
    /// <param name="expected">The expected ASCII bytes without a null terminator.</param>
    /// <returns><see langword="true"/> when the complete string matches <paramref name="expected"/>.</returns>
    public bool ReadNullTerminatedStringEquals(ReadOnlySpan<byte> expected)
    {
        int index = 0;
        bool equals = true;
        while (this.Remaining > 0)
        {
            byte value = this.ReadByte();
            if (value == 0)
            {
                return equals && index == expected.Length;
            }

            if ((uint)index >= (uint)expected.Length || value != expected[index])
            {
                equals = false;
            }

            index++;
        }

        throw new InvalidImageContentException($"The {this.name} payload contains an unterminated string.");
    }

    /// <summary>
    /// Refills the reusable buffer without reading beyond the bounded payload.
    /// </summary>
    /// <param name="required">The number of contiguous bytes required by the next value.</param>
    private void Ensure(int required)
    {
        int buffered = this.count - this.offset;
        if (buffered >= required)
        {
            return;
        }

        if (buffered > 0)
        {
            // Preserve an incomplete fixed-width value at the start of the buffer before the next read.
            this.buffer.Slice(this.offset, buffered).CopyTo(this.buffer);
        }

        this.offset = 0;
        this.count = buffered;
        while (this.count < required && this.remaining > 0)
        {
            int requested = (int)Math.Min(this.buffer.Length - this.count, this.remaining);
            int read = this.stream.Read(this.buffer.Slice(this.count, requested));
            if (read == 0)
            {
                throw new InvalidImageContentException($"The {this.name} payload is truncated.");
            }

            this.count += read;
            this.remaining -= read;
        }

        if (this.count < required)
        {
            throw new InvalidImageContentException($"The {this.name} payload is truncated.");
        }
    }
}
