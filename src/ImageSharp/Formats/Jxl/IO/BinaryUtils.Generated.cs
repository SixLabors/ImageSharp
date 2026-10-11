// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Jxl.IO;

/// <summary>
/// Reads primitives from streams with correct endianness.
/// </summary>
// TODO: move this class into the IO or Common folder?
internal static class BinaryUtils
{
    /// <summary>
    /// Reads a <see cref="Int16" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int16" /> will be read from.</param>
    /// <returns><see cref="Int16" /></returns>
    public static Int16 ReadInt16LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int16)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt16LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="Int16" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int16" /> will be read from.</param>
    /// <returns><see cref="Int16" /></returns>
    public static Int16 ReadInt16BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int16)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt16BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="Int16" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int16" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt16LittleEndian(Stream stream, Int16 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int16)];
        BinaryPrimitives.WriteInt16LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="Int16" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int16" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt16BigEndian(Stream stream, Int16 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int16)];
        BinaryPrimitives.WriteInt16BigEndian(data, value);
        stream.Write(data);
    }
    /// <summary>
    /// Reads a <see cref="UInt16" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt16" /> will be read from.</param>
    /// <returns><see cref="UInt16" /></returns>
    public static UInt16 ReadUInt16LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt16)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt16LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="UInt16" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt16" /> will be read from.</param>
    /// <returns><see cref="UInt16" /></returns>
    public static UInt16 ReadUInt16BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt16)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt16BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt16" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt16" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt16LittleEndian(Stream stream, UInt16 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt16)];
        BinaryPrimitives.WriteUInt16LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt16" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt16" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt16BigEndian(Stream stream, UInt16 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt16)];
        BinaryPrimitives.WriteUInt16BigEndian(data, value);
        stream.Write(data);
    }
    /// <summary>
    /// Reads a <see cref="Int32" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int32" /> will be read from.</param>
    /// <returns><see cref="Int32" /></returns>
    public static Int32 ReadInt32LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int32)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt32LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="Int32" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int32" /> will be read from.</param>
    /// <returns><see cref="Int32" /></returns>
    public static Int32 ReadInt32BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int32)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt32BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="Int32" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int32" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt32LittleEndian(Stream stream, Int32 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int32)];
        BinaryPrimitives.WriteInt32LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="Int32" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int32" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt32BigEndian(Stream stream, Int32 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int32)];
        BinaryPrimitives.WriteInt32BigEndian(data, value);
        stream.Write(data);
    }
    /// <summary>
    /// Reads a <see cref="UInt32" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt32" /> will be read from.</param>
    /// <returns><see cref="UInt32" /></returns>
    public static UInt32 ReadUInt32LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt32)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="UInt32" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt32" /> will be read from.</param>
    /// <returns><see cref="UInt32" /></returns>
    public static UInt32 ReadUInt32BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt32)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt32BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt32" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt32" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt32LittleEndian(Stream stream, UInt32 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt32)];
        BinaryPrimitives.WriteUInt32LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt32" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt32" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt32BigEndian(Stream stream, UInt32 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt32)];
        BinaryPrimitives.WriteUInt32BigEndian(data, value);
        stream.Write(data);
    }
    /// <summary>
    /// Reads a <see cref="Int64" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int64" /> will be read from.</param>
    /// <returns><see cref="Int64" /></returns>
    public static Int64 ReadInt64LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int64)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt64LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="Int64" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int64" /> will be read from.</param>
    /// <returns><see cref="Int64" /></returns>
    public static Int64 ReadInt64BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(Int64)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadInt64BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="Int64" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int64" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt64LittleEndian(Stream stream, Int64 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int64)];
        BinaryPrimitives.WriteInt64LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="Int64" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="Int64" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteInt64BigEndian(Stream stream, Int64 value)
    {
        Span<byte> data = stackalloc byte[sizeof(Int64)];
        BinaryPrimitives.WriteInt64BigEndian(data, value);
        stream.Write(data);
    }
    /// <summary>
    /// Reads a <see cref="UInt64" />
    /// from the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt64" /> will be read from.</param>
    /// <returns><see cref="UInt64" /></returns>
    public static UInt64 ReadUInt64LittleEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt64)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt64LittleEndian(data);
    }

    /// <summary>
    /// Reads a <see cref="UInt64" />
    /// from the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt64" /> will be read from.</param>
    /// <returns><see cref="UInt64" /></returns>
    public static UInt64 ReadUInt64BigEndian(Stream stream)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt64)];
        stream.ReadExactly(data);
        return BinaryPrimitives.ReadUInt64BigEndian(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt64" />
    /// into the specified stream in little-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt64" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt64LittleEndian(Stream stream, UInt64 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt64)];
        BinaryPrimitives.WriteUInt64LittleEndian(data, value);
        stream.Write(data);
    }

    /// <summary>
    /// Writes a <see cref="UInt64" />
    /// into the specified stream in big-endian order.
    /// </summary>
    /// <param name="stream">The stream where the <see cref="UInt64" /> will be written to.</param>
    /// <param name="value">Value which will be written to the stream.</param>
    public static void WriteUInt64BigEndian(Stream stream, UInt64 value)
    {
        Span<byte> data = stackalloc byte[sizeof(UInt64)];
        BinaryPrimitives.WriteUInt64BigEndian(data, value);
        stream.Write(data);
    }
}
