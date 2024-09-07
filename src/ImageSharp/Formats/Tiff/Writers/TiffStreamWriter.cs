// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

/// <summary>
/// Utility class for writing TIFF data to a <see cref="Stream"/>.
/// </summary>
internal sealed class TiffStreamWriter : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffStreamWriter"/> class.
    /// </summary>
    /// <param name="output">The output stream.</param>
    public TiffStreamWriter(Stream output) => this.BaseStream = output;

    /// <summary>
    /// Gets a value indicating whether the architecture is little-endian.
    /// </summary>
    public static bool IsLittleEndian => BitConverter.IsLittleEndian;

    /// <summary>
    /// Gets the current position within the stream.
    /// </summary>
    public long Position => this.BaseStream.Position;

    /// <summary>
    /// Gets the base stream.
    /// </summary>
    public Stream BaseStream { get; }

    /// <summary>
    /// Writes an empty four bytes to the stream, returning the offset to be written later.
    /// </summary>
    /// <param name="buffer">Scratch buffer with minimum size of 4.</param>
    /// <returns>The offset to be written later.</returns>
    public long PlaceMarker(Span<byte> buffer)
    {
        long offset = this.BaseStream.Position;
        this.Write(0u, buffer);
        return offset;
    }

    /// <summary>
    /// Writes an array of bytes to the current stream.
    /// </summary>
    /// <param name="value">The bytes to write.</param>
    public void Write(byte[] value) => this.BaseStream.Write(value, 0, value.Length);

    /// <summary>
    /// Writes the specified value.
    /// </summary>
    /// <param name="value">The bytes to write.</param>
    public void Write(ReadOnlySpan<byte> value) => this.BaseStream.Write(value);

    /// <summary>
    /// Writes a byte to the current stream.
    /// </summary>
    /// <param name="value">The byte to write.</param>
    public void Write(byte value) => this.BaseStream.WriteByte(value);

    /// <summary>
    /// Writes a two-byte unsigned integer to the current stream.
    /// </summary>
    /// <param name="value">The two-byte unsigned integer to write.</param>
    /// <param name="buffer">Scratch buffer with minimum size of 2.</param>
    public void Write(ushort value, Span<byte> buffer)
    {
        if (IsLittleEndian)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        }

        this.BaseStream.Write(buffer.Slice(0, 2));
    }

    /// <summary>
    /// Writes a four-byte unsigned integer to the current stream.
    /// </summary>
    /// <param name="value">The four-byte unsigned integer to write.</param>
    /// <param name="buffer">Scratch buffer with minimum size of 4.</param>
    public void Write(uint value, Span<byte> buffer)
    {
        if (IsLittleEndian)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        }
        else
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        }

        this.BaseStream.Write(buffer.Slice(0, 4));
    }

    /// <summary>
    /// Writes an array of bytes to the current stream, padded to four-bytes.
    /// </summary>
    /// <param name="value">The bytes to write.</param>
    public void WritePadded(Span<byte> value)
    {
        this.BaseStream.Write(value);

        if (value.Length % 4 != 0)
        {
            // No allocation occurs, refers directly to assembly's data segment.
            ReadOnlySpan<byte> paddingBytes = [0x00, 0x00, 0x00, 0x00];
            paddingBytes = paddingBytes[..(4 - (value.Length % 4))];
            this.BaseStream.Write(paddingBytes);
        }
    }

    /// <summary>
    /// Writes a four-byte unsigned integer to the specified marker in the stream.
    /// </summary>
    /// <param name="offset">The offset returned when placing the marker</param>
    /// <param name="value">The four-byte unsigned integer to write.</param>
    /// <param name="buffer">Scratch buffer.</param>
    public void WriteMarker(long offset, uint value, Span<byte> buffer)
    {
        long back = this.BaseStream.Position;
        this.BaseStream.Seek(offset, SeekOrigin.Begin);
        this.Write(value, buffer);
        this.BaseStream.Seek(back, SeekOrigin.Begin);
    }

    public void WriteMarkerFast(long offset, uint value, Span<byte> buffer)
    {
        this.BaseStream.Seek(offset, SeekOrigin.Begin);
        this.Write(value, buffer);
    }

    /// <summary>
    /// Disposes <see cref="TiffStreamWriter"/> instance, ensuring any unwritten data is flushed.
    /// </summary>
    public void Dispose() => this.BaseStream.Flush();
}
