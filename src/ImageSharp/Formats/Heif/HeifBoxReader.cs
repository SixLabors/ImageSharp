// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Reads bounded ISO BMFF box headers and payloads used by the HEIF image container.
/// </summary>
internal readonly struct HeifBoxReader
{
    /// <summary>
    /// The allocator used for payloads that must be materialized while parsing.
    /// </summary>
    private readonly MemoryAllocator allocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifBoxReader"/> struct.
    /// </summary>
    /// <param name="allocator">The allocator used for bounded payload buffers.</param>
    public HeifBoxReader(MemoryAllocator allocator) => this.allocator = allocator;

    /// <summary>
    /// Reads an ISO BMFF box header using caller-owned scratch and resolves its validated payload length.
    /// </summary>
    /// <param name="stream">The stream positioned at the box size field.</param>
    /// <param name="parentEndPosition">The absolute end position of the containing box or file.</param>
    /// <param name="scratch">Caller-owned scratch containing at least eight bytes.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <param name="topLevel">Indicates whether a size-zero box may extend to the end of the file.</param>
    /// <returns>The number of payload bytes following the complete variable-length header.</returns>
    public static long ReadHeader(Stream stream, long parentEndPosition, Span<byte> scratch, out Heif4CharCode boxType, bool topLevel = false)
    {
        if (parentEndPosition - stream.Position < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        Span<byte> buffer = scratch[..8];
        ReadExactly(stream, buffer, "Not enough data to read the box header.");

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int headerSize = 8;
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);

        if (boxSize == 1)
        {
            if (parentEndPosition - stream.Position < 8)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            ReadExactly(stream, buffer, "Not enough data to read the extended box size.");
            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buffer);
            headerSize += 8;
        }

        if (boxType == Heif4CharCode.Uuid)
        {
            if (parentEndPosition - stream.Position < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the UUID box user type.");
            }

            // The UUID user type belongs to the variable box header even though the bounded image parser does not
            // interpret it. Advance here so every caller receives the actual payload start and length.
            Skip(stream, 16);
            headerSize += 16;
        }

        if (boxSize == 0)
        {
            if (!topLevel)
            {
                throw new InvalidImageContentException("A nested box cannot extend to the end of the file.");
            }

            return parentEndPosition - stream.Position;
        }

        if (boxSize < (ulong)headerSize)
        {
            throw new InvalidImageContentException("Box size is smaller than its header.");
        }

        ulong contentLength = boxSize - (ulong)headerSize;
        if (contentLength > (ulong)(parentEndPosition - stream.Position))
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
        }

        return (long)contentLength;
    }

    /// <summary>
    /// Parses an ISO BMFF child-box header from a bounded parent payload.
    /// </summary>
    /// <param name="buffer">The remaining bytes in the parent payload, beginning at the child size field.</param>
    /// <param name="length">Receives the validated child payload length.</param>
    /// <param name="boxType">Receives the child box four-character code.</param>
    /// <returns>The number of bytes occupied by the complete child header.</returns>
    public static int ParseHeader(ReadOnlySpan<byte> buffer, out long length, out Heif4CharCode boxType)
    {
        if (buffer.Length < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int bytesRead = 8;
        boxType = (Heif4CharCode)BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
        if (boxSize == 1)
        {
            if (buffer.Length < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            bytesRead += 8;
        }

        if (boxType == Heif4CharCode.Uuid)
        {
            if (buffer.Length - bytesRead < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the UUID box user type.");
            }

            bytesRead += 16;
        }

        if (boxSize == 0)
        {
            throw new InvalidImageContentException("A nested box cannot extend to the end of the file.");
        }

        if (boxSize < (ulong)bytesRead)
        {
            throw new InvalidImageContentException("Box size is smaller than its header.");
        }

        ulong contentLength = boxSize - (ulong)bytesRead;
        if (contentLength > (ulong)(buffer.Length - bytesRead))
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
        }

        length = (long)contentLength;
        return bytesRead;
    }

    /// <summary>
    /// Reads a complete bounded box payload into allocator-owned memory.
    /// </summary>
    /// <param name="stream">The stream positioned at the payload start.</param>
    /// <param name="length">The validated payload length.</param>
    /// <returns>An owner containing exactly the requested payload bytes.</returns>
    public IMemoryOwner<byte> ReadPayload(Stream stream, long length)
    {
        if ((ulong)length > int.MaxValue)
        {
            throw new InvalidImageContentException("Box content is too large to buffer.");
        }

        int bufferLength = (int)length;
        IMemoryOwner<byte> memory = this.allocator.Allocate<byte>(bufferLength);
        try
        {
            ReadExactly(stream, memory.GetSpan(), "Stream length is not sufficient for box content.");
            return memory;
        }
        catch
        {
            memory.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Advances over a validated box payload without narrowing its 64-bit length.
    /// </summary>
    /// <param name="stream">The seekable container stream.</param>
    /// <param name="length">The validated payload length.</param>
    public static void Skip(Stream stream, long length) => stream.Seek(length, SeekOrigin.Current);

    /// <summary>
    /// Validates a child payload length against the bytes remaining in its parent.
    /// </summary>
    /// <param name="length">The declared child payload length.</param>
    /// <param name="parentLength">The number of bytes remaining in the parent.</param>
    public static void EnsureInsideParent(long length, long parentLength)
    {
        if (length < 0 || parentLength < 0 || length > parentLength)
        {
            throw new InvalidImageContentException("Box size extends beyond its parent boundary.");
        }
    }

    /// <summary>
    /// Reads exactly the requested number of bytes or rejects the truncated payload.
    /// </summary>
    /// <param name="stream">The source stream.</param>
    /// <param name="destination">The complete destination span.</param>
    /// <param name="message">The malformed-image message used when the stream ends early.</param>
    public static void ReadExactly(Stream stream, Span<byte> destination, string message)
    {
        int offset = 0;
        while (offset < destination.Length)
        {
            int read = stream.Read(destination[offset..]);
            if (read == 0)
            {
                throw new InvalidImageContentException(message);
            }

            offset += read;
        }
    }
}
