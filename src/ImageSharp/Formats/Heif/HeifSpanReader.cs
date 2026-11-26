// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Reads the fields of one bounded HEIF box payload with a sticky error.
/// </summary>
/// <remarks>
/// A read past the end of the payload returns zero. It also sets the error and moves the position to the end of the payload. Thus a parser reads all of its
/// fields and then calls <see cref="EnsureEnd"/> one time. That call reports a truncated payload and a payload with unread bytes.
/// </remarks>
internal ref struct HeifSpanReader
{
    /// <summary>
    /// The flag mask that accepts every value of the 24-bit full-box flags.
    /// </summary>
    public const uint AnyFlags = 0x00FFFFFF;

    /// <summary>
    /// The complete box payload.
    /// </summary>
    private readonly ReadOnlySpan<byte> data;

    /// <summary>
    /// The box name that the error messages use.
    /// </summary>
    private readonly string name;

    /// <summary>
    /// The offset of the next unread byte.
    /// </summary>
    private int position;

    /// <summary>
    /// A value that is <see langword="true"/> if a read went past the end of the payload.
    /// </summary>
    private bool overrun;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSpanReader"/> struct.
    /// </summary>
    /// <param name="data">The complete box payload.</param>
    /// <param name="name">The box name that the error messages use.</param>
    public HeifSpanReader(ReadOnlySpan<byte> data, string name)
    {
        this.data = data;
        this.name = name;
        this.position = 0;
        this.overrun = false;
    }

    /// <summary>
    /// Gets the number of unread bytes.
    /// </summary>
    public readonly int Remaining => this.data.Length - this.position;

    /// <summary>
    /// Gets the unread bytes. A parser uses them to read the header of a child box.
    /// </summary>
    public readonly ReadOnlySpan<byte> RemainingData => this.data[this.position..];

    /// <summary>
    /// Gets a value indicating whether the reader read all of the payload and no read went past its end.
    /// </summary>
    public readonly bool IsComplete => !this.overrun && this.position == this.data.Length;

    /// <summary>
    /// Reads one unsigned byte.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public byte ReadUInt8() => this.TryTake(1, out ReadOnlySpan<byte> bytes) ? bytes[0] : (byte)0;

    /// <summary>
    /// Reads one big-endian unsigned 16-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public ushort ReadUInt16() => this.TryTake(2, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : (ushort)0;

    /// <summary>
    /// Reads one big-endian signed 16-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public short ReadInt16() => this.TryTake(2, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadInt16BigEndian(bytes) : (short)0;

    /// <summary>
    /// Reads one big-endian unsigned 32-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public uint ReadUInt32() => this.TryTake(4, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : 0;

    /// <summary>
    /// Reads one big-endian signed 32-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public int ReadInt32() => this.TryTake(4, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadInt32BigEndian(bytes) : 0;

    /// <summary>
    /// Reads one big-endian unsigned 64-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public ulong ReadUInt64() => this.TryTake(8, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadUInt64BigEndian(bytes) : 0;

    /// <summary>
    /// Reads one big-endian signed 64-bit value.
    /// </summary>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public long ReadInt64() => this.TryTake(8, out ReadOnlySpan<byte> bytes) ? BinaryPrimitives.ReadInt64BigEndian(bytes) : 0;

    /// <summary>
    /// Reads a 16-bit or a 32-bit unsigned value. The box version or flags select the width.
    /// </summary>
    /// <param name="large"><see langword="true"/> if the field has 32 bits.</param>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public uint ReadU16Or32(bool large) => large ? this.ReadUInt32() : this.ReadUInt16();

    /// <summary>
    /// Reads an unsigned value of 0, 1, 2, 4 or 8 bytes. A width of zero gives the implicit value zero.
    /// </summary>
    /// <param name="size">The field width in bytes.</param>
    /// <returns>The value, or zero after the end of the payload.</returns>
    public ulong ReadUInt(int size)
    {
        DebugGuard.IsTrue(size is 0 or 1 or 2 or 4 or 8, nameof(size), "The field width is not supported.");
        return size switch
        {
            0 => 0,
            1 => this.ReadUInt8(),
            2 => this.ReadUInt16(),
            4 => this.ReadUInt32(),
            _ => this.ReadUInt64()
        };
    }

    /// <summary>
    /// Reads a null-terminated UTF-8 string.
    /// </summary>
    /// <returns>The string without its terminator, or an empty string when the payload has no terminator.</returns>
    public string ReadCString()
    {
        ReadOnlySpan<byte> remaining = this.RemainingData;
        int terminator = remaining.IndexOf((byte)0);
        if (terminator < 0)
        {
            this.SetOverrun();
            return string.Empty;
        }

        this.position += terminator + 1;
        return Encoding.UTF8.GetString(remaining[..terminator]);
    }

    /// <summary>
    /// Reads a span of bytes.
    /// </summary>
    /// <param name="count">The number of bytes.</param>
    /// <returns>The bytes, or an empty span when the payload holds fewer bytes.</returns>
    public ReadOnlySpan<byte> ReadBytes(int count) => this.TryTake(count, out ReadOnlySpan<byte> bytes) ? bytes : default;

    /// <summary>
    /// Moves past bytes that the parser does not use.
    /// </summary>
    /// <param name="count">The number of bytes.</param>
    public void Skip(int count) => this.TryTake(count, out _);

    /// <summary>
    /// Reads the version and flags of a full box, and validates them.
    /// </summary>
    /// <remarks>ISO/IEC 14496-12, section 4.2: a full box starts with an 8-bit version and 24-bit flags.</remarks>
    /// <param name="maxVersion">The largest supported version.</param>
    /// <param name="allowedFlags">The flag bits that the parser accepts.</param>
    /// <param name="flags">Receives the flags.</param>
    /// <returns>The version.</returns>
    /// <exception cref="InvalidImageContentException">The box uses an unsupported version or flag.</exception>
    public byte ReadFullBoxHeader(byte maxVersion, uint allowedFlags, out uint flags)
    {
        uint versionAndFlags = this.ReadUInt32();
        byte version = (byte)(versionAndFlags >> 24);
        flags = versionAndFlags & 0x00FFFFFF;
        if (version > maxVersion)
        {
            throw new InvalidImageContentException($"The {this.name} box has unsupported version {version}.");
        }

        if ((flags & ~allowedFlags) != 0)
        {
            throw new InvalidImageContentException($"The {this.name} box has unsupported flags.");
        }

        return version;
    }

    /// <summary>
    /// Reads an entry count and validates that the payload can hold the entries.
    /// </summary>
    /// <remarks>
    /// This validation makes the payload length the limit of each loop over the entries. Thus a small payload cannot cause a loop of up to 2^32 iterations. It
    /// also cannot cause an allocation of the declared count.
    /// </remarks>
    /// <param name="countSize">The width of the count field in bytes.</param>
    /// <param name="entrySize">The smallest number of bytes of one entry.</param>
    /// <returns>The entry count, or zero and a sticky error when the payload cannot hold the entries.</returns>
    public uint ReadEntryCount(int countSize, int entrySize)
    {
        DebugGuard.MustBeGreaterThan(entrySize, 0, nameof(entrySize));
        uint count = (uint)this.ReadUInt(countSize);
        if ((ulong)count * (uint)entrySize > (ulong)this.Remaining)
        {
            this.SetOverrun();
            return 0;
        }

        return count;
    }

    /// <summary>
    /// Validates that no read went past the end of the payload.
    /// </summary>
    /// <remarks>
    /// A read past the end returns zero. A parser calls this method before it validates a value that can be zero because of a truncation. Thus the error names
    /// the truncation and not the zero value.
    /// </remarks>
    /// <exception cref="InvalidImageContentException">The payload is truncated.</exception>
    public readonly void EnsureNotTruncated()
    {
        if (this.overrun)
        {
            throw new InvalidImageContentException($"The {this.name} box is truncated.");
        }
    }

    /// <summary>
    /// Validates that the reader read all of the payload and that no read went past its end.
    /// </summary>
    /// <exception cref="InvalidImageContentException">The payload is truncated or has unread bytes.</exception>
    public readonly void EnsureEnd()
    {
        this.EnsureNotTruncated();
        if (this.position != this.data.Length)
        {
            throw new InvalidImageContentException($"The {this.name} box contains unexpected trailing data.");
        }
    }

    /// <summary>
    /// Takes the next bytes of the payload.
    /// </summary>
    /// <param name="count">The number of bytes.</param>
    /// <param name="bytes">Receives the bytes.</param>
    /// <returns><see langword="true"/> when the payload holds the bytes. Otherwise <see langword="false"/>, and the method sets the error.</returns>
    private bool TryTake(int count, out ReadOnlySpan<byte> bytes)
    {
        if ((uint)count > (uint)this.Remaining)
        {
            this.SetOverrun();
            bytes = default;
            return false;
        }

        bytes = this.data.Slice(this.position, count);
        this.position += count;
        return true;
    }

    /// <summary>
    /// Sets the sticky error and moves the position to the end of the payload.
    /// </summary>
    private void SetOverrun()
    {
        this.overrun = true;
        this.position = this.data.Length;
    }
}
