// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;

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
}
