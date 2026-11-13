// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="Stream"/> type.
/// </summary>
internal static class StreamExtensions
{
    /// <summary>
    /// Writes data from a stream from the provided buffer.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset within the buffer to begin writing.</param>
    /// <param name="count">The number of bytes to write to the stream.</param>
    public static void Write(this Stream stream, Span<byte> buffer, int offset, int count)
        => stream.Write(buffer.Slice(offset, count));

    /// <summary>
    /// Reads data from a stream into the provided buffer.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset within the buffer where the bytes are read into.</param>
    /// <param name="count">The number of bytes, if available, to read.</param>
    /// <returns>The actual number of bytes read.</returns>
    public static int Read(this Stream stream, Span<byte> buffer, int offset, int count)
        => stream.Read(buffer.Slice(offset, count));

    /// <summary>
    /// Skips the number of bytes in the given stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="count">A byte offset relative to the origin parameter.</param>
    public static void Skip(this Stream stream, int count)
    {
        if (count < 1)
        {
            return;
        }

        if (stream.CanSeek)
        {
            stream.Seek(count, SeekOrigin.Current);
            return;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            while (count > 0)
            {
                int bytesRead = stream.Read(buffer, 0, count);
                if (bytesRead == 0)
                {
                    break;
                }

                count -= bytesRead;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Fills the buffer. The method stops if the stream returns no bytes.
    /// </summary>
    /// <param name="stream">The stream positioned at the input.</param>
    /// <param name="destination">The buffer to fill.</param>
    /// <returns>Whether the complete buffer was read.</returns>
    public static bool TryReadExactly(this Stream stream, Span<byte> destination)
    {
        while (!destination.IsEmpty)
        {
            int read = stream.Read(destination);
            if (read == 0)
            {
                return false;
            }

            destination = destination[read..];
        }

        return true;
    }

    /// <summary>
    /// Appends a complete segment. The first segment uses an array of the exact length.
    /// </summary>
    /// <param name="stream">The input positioned at the segment.</param>
    /// <param name="destination">The array that holds the complete segments.</param>
    /// <param name="written">The number of bytes from complete segments.</param>
    /// <param name="length">The segment length.</param>
    /// <param name="maxLength">The maximum combined length of all segments.</param>
    /// <returns>Whether the segment fits and the method reads all its bytes.</returns>
    /// <remarks>
    /// If the length is out of range, skip only the segment bytes available in the stream.
    /// If a read stops early, the stream stays after the last byte read. The byte count stays the same.
    /// </remarks>
    public static bool TryAppendExactly(this Stream stream, ref byte[]? destination, ref int written, int length, int maxLength)
    {
        long available = stream.Length - stream.Position;
        if (length < 0 || length > available || length > maxLength - written)
        {
            // The caller can continue at the next segment if its bytes are present.
            stream.Position += Math.Min(Math.Max(0, length), Math.Max(0, available));
            return false;
        }

        int required = written + length;
        byte[] buffer = destination ?? Array.Empty<byte>();
        if (required > buffer.Length)
        {
            // Grow geometrically only for split packets. A single segment keeps its first array.
            int capacity = (int)Math.Min((long)buffer.Length * 2, maxLength);
            byte[] expanded = new byte[Math.Max(required, capacity)];
            buffer.AsSpan(0, written).CopyTo(expanded);
            buffer = expanded;
        }

        if (!stream.TryReadExactly(buffer.AsSpan(written, length)))
        {
            return false;
        }

        destination = buffer;
        written = required;
        return true;
    }

    /// <summary>
    /// Reads a bounded byte range into an array owned by the caller.
    /// </summary>
    /// <param name="stream">The input positioned at the start of the range.</param>
    /// <param name="length">The length of the range.</param>
    /// <param name="data">The bytes read, or null if the range could not be read.</param>
    /// <returns>Whether the method reads the entire range.</returns>
    /// <remarks>
    /// If the length is out of range, the stream position does not change.
    /// If a read stops early, the stream stays after the last byte read.
    /// </remarks>
    public static bool TryReadExactly(this Stream stream, long length, [NotNullWhen(true)] out byte[]? data)
    {
        data = null;
        if (length < 0 || length > Array.MaxLength || length > stream.Length - stream.Position)
        {
            return false;
        }

        byte[] bytes = length == 0 ? Array.Empty<byte>() : new byte[(int)length];
        if (!stream.TryReadExactly(bytes))
        {
            return false;
        }

        data = bytes;
        return true;
    }
}
