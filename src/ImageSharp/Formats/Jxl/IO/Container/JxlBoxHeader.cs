// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Container;

/// <summary>
/// Stores a JPEG XL container box's type, total size, and header width.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct JxlBoxHeader
{
    /// <summary>
    /// Total box size in bytes, including its header. Zero extends the box through the end of the input.
    /// </summary>
    public ulong Size;

    /// <summary>
    /// Type of the box.
    /// </summary>
    public uint Type;

    /// <summary>
    /// Whether the box extends through the end of the input. This box cannot be followed by another box.
    /// </summary>
    public bool SizeExtendsTillEnd;

    /// <summary>
    /// Whether the header contains an explicit 64-bit size after its type, even when that size is zero.
    /// </summary>
    public bool ContainsLargeSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBoxHeader"/> struct.
    /// </summary>
    /// <param name="size">The total size, including the 8-byte or 16-byte header. Zero denotes an unbounded box.</param>
    /// <param name="type">The type of the box.</param>
    /// <param name="sizeExtendsTillEnd">Does the box size extend till the end of the file?</param>
    /// <param name="containsLargeSize">Is there a 64-bit size field?</param>
    public JxlBoxHeader(ulong size, uint type, bool sizeExtendsTillEnd, bool containsLargeSize)
    {
        this.Size = size;
        this.Type = type;
        this.SizeExtendsTillEnd = sizeExtendsTillEnd;
        this.ContainsLargeSize = containsLargeSize;
    }

    /// <summary>
    /// Converts a 4-character ASCII string (e.g. "jxlc") into a uint type code.
    /// </summary>
    /// <param name="typeString">Input type string to convert</param>
    /// <returns>Unsigned integer representation of the type string</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint TypeFromString(string typeString)
    {
        if (typeString.Length != 4)
        {
            throw new ArgumentException("Box type must be exactly 4 characters", nameof(typeString));
        }

        return ((uint)typeString[0] << 24) |
           ((uint)typeString[1] << 16) |
           ((uint)typeString[2] << 8) |
           typeString[3];
    }

    /// <summary>
    /// Converts a uint type code back into a 4-character ASCII string.
    /// </summary>
    /// <param name="typeCode">Unsigned integer representation of the type string</param>
    /// <returns>The string representing the type code.</returns>
    public static string TypeToString(uint typeCode)
    {
        return typeCode switch
        {
            0x6A786C20 => "jxl ",
            0x6A786C70 => "jxlp",
            0x6A786C63 => "jxlc",
            0x66747970 => "ftyp",
            0x6A627264 => "jbrd",
            0x45786966 => "Exif",
            0x786D6C20 => "xml ",
            0x6A756D62 => "jumb",
            _ => Fallback(typeCode)
        };

        static string Fallback(uint typeCode)
        {
            // The box type is not known
            Span<byte> buffer = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(buffer, typeCode);

            return Encoding.ASCII.GetString(buffer);
        }
    }

    /// <summary>
    /// Reads the JPEG XL box's size and type, followed by its extended size when present.
    /// </summary>
    /// <param name="stream">A stream to parse the header from.</param>
    /// <returns>The box header.</returns>
    /// <exception cref="InvalidOperationException">A nonzero box size is smaller than its header.</exception>
    /// <exception cref="EndOfStreamException">The input ends before the complete header.</exception>
    public static JxlBoxHeader ReadHeader(Stream stream)
    {
        // Every header begins with size32 and type. The value one selects a following size64;
        // it does not move the type behind that field. All integers use big-endian byte order.
        ulong size = BinaryUtils.ReadUInt32BigEndian(stream);
        uint type = BinaryUtils.ReadUInt32BigEndian(stream);
        bool haveSize64 = size == 1;

        if (haveSize64)
        {
            size = BinaryUtils.ReadUInt64BigEndian(stream);
        }

        // Match the native container parser: zero remains unbounded with either header width.
        // Every bounded size includes at least the complete header, so later payload subtraction cannot underflow.
        ulong headerSize = haveSize64 ? 16UL : 8UL;
        if (size != 0 && size < headerSize)
        {
            throw new InvalidOperationException("JPEG XL box size is smaller than its header.");
        }

        return new JxlBoxHeader(size, type, size == 0, haveSize64);
    }

    /// <summary>
    /// Writes the box header in big-endian order and retains an explicitly selected extended header.
    /// </summary>
    /// <param name="writer">The stream to write the box header to.</param>
    public readonly void WriteHeader(Stream writer)
    {
        ulong size = this.SizeExtendsTillEnd ? 0 : this.Size;
        bool haveSize64 = this.ContainsLargeSize || size > uint.MaxValue;

        // Preserve the header width of parsed boxes, including an extended zero-size final box.
        // The type always occupies bytes 4 through 7; an optional extended size occupies bytes 8 through 15.
        BinaryUtils.WriteUInt32BigEndian(writer, haveSize64 ? 1U : (uint)size);
        BinaryUtils.WriteUInt32BigEndian(writer, this.Type);

        if (haveSize64)
        {
            BinaryUtils.WriteUInt64BigEndian(writer, size);
        }
    }
}
