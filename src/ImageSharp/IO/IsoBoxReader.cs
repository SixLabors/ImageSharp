// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.IO;

/// <summary>
/// Reads bounded ISO base media file format box headers shared by image containers.
/// </summary>
internal static class IsoBoxReader
{
    /// <summary>
    /// The UUID box type. Its sixteen-byte user type belongs to the header, not the payload.
    /// </summary>
    private const uint UuidType = 0x75756964U;

    /// <summary>
    /// Reads an ISO BMFF box header into a caller-owned buffer and returns the validated payload length.
    /// </summary>
    /// <param name="stream">The stream positioned at the box size field.</param>
    /// <param name="parentEndPosition">The absolute end position of the containing box or file.</param>
    /// <param name="headerBuffer">A caller-owned buffer of at least eight bytes that receives the header.</param>
    /// <param name="boxType">Receives the box four-character code.</param>
    /// <param name="topLevel">Indicates whether a size-zero box can extend to the end of the file.</param>
    /// <returns>The number of payload bytes following the complete variable-length header.</returns>
    public static long ReadHeader(Stream stream, long parentEndPosition, Span<byte> headerBuffer, out uint boxType, bool topLevel)
    {
        if (parentEndPosition - stream.Position < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        Span<byte> buffer = headerBuffer[..8];
        ReadExactly(stream, buffer, "Not enough data to read the box header.");

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int headerSize = 8;
        boxType = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);

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

        if (boxType == UuidType)
        {
            if (parentEndPosition - stream.Position < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the UUID box user type.");
            }

            // The UUID user type belongs to the variable box header even though the bounded image parser does not
            // interpret it. Advance here so every caller receives the actual payload start and length.
            stream.Seek(16, SeekOrigin.Current);
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
    public static int ParseHeader(ReadOnlySpan<byte> buffer, out long length, out uint boxType)
    {
        if (buffer.Length < 8)
        {
            throw new InvalidImageContentException("Not enough data to read the box header.");
        }

        ulong boxSize = BinaryPrimitives.ReadUInt32BigEndian(buffer);
        int bytesRead = 8;
        boxType = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
        if (boxSize == 1)
        {
            if (buffer.Length < 16)
            {
                throw new InvalidImageContentException("Not enough data to read the extended box size.");
            }

            boxSize = BinaryPrimitives.ReadUInt64BigEndian(buffer[bytesRead..]);
            bytesRead += 8;
        }

        if (boxType == UuidType)
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
