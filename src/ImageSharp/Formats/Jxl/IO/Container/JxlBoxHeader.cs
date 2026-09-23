// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Container;

/// <summary>
/// Header for JPEG XL container format.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 16)]
internal struct JxlBoxHeader
{
    /// <summary>
    /// Box size in bytes.
    /// </summary>
    public ulong Size;

    /// <summary>
    /// Type of the box.
    /// </summary>
    public uint Type;

    /// <summary>
    /// True if the size field extends until the end of the file.
    /// </summary>
    public bool SizeExtendsTillEnd;

    /// <summary>
    /// True if the size is 64-bit.
    /// </summary>
    public bool ContainsLargeSize;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBoxHeader"/> struct.
    /// </summary>
    /// <param name="size">The size of the box.</param>
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
    /// Parses the JPEG XL box header.
    /// </summary>
    /// <param name="stream">A stream to parse the header from.</param>
    /// <returns>The box header.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the header is invalid.</exception>
    public static JxlBoxHeader ReadHeader(Stream stream)
    {
        ulong size = BinaryUtils.ReadUInt32BigEndian(stream);
        bool haveSize64 = false;

        if (size == 1)
        {
            // When the size value is equal to 1, a new 64-bit
            // size field follows.
            haveSize64 = true;
            size = BinaryUtils.ReadUInt64BigEndian(stream);
        }

        // Read the 4-byte type field.
        uint type = BinaryUtils.ReadUInt32BigEndian(stream);

        if (haveSize64)
        {
            // When the 64-bit largesize was read,
            // the size cannot proceed till the end of the file.
            if (size is 0 or 1)
            {
                throw new InvalidOperationException("Large size cannot have another large size or extend till the end of the file");
            }

            return new JxlBoxHeader(size, type, sizeExtendsTillEnd: false, containsLargeSize: true);
        }
        else
        {
            return new JxlBoxHeader(size, type, sizeExtendsTillEnd: size == 0, containsLargeSize: false);
        }
    }

    /// <summary>
    /// Writes the box header to the specified stream.
    /// </summary>
    /// <param name="writer">The stream to write the box header to.</param>
    public readonly void WriteHeader(Stream writer)
    {
        if (this.Size is > uint.MaxValue or 1)
        {
            BinaryUtils.WriteUInt32BigEndian(writer, 1); // Indicates a large size is present
            BinaryUtils.WriteUInt64BigEndian(writer, this.Size);
        }
        else
        {
            BinaryUtils.WriteUInt32BigEndian(writer, (uint)this.Size);
        }

        BinaryUtils.WriteUInt32BigEndian(writer, this.Type);
    }
}
