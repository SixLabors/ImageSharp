// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp;

/// <summary>
/// Extension methods for the <see cref="BufferedReadStream"/> type.
/// </summary>
internal static class BufferedReadStreamExtensions
{
    /// <summary>
    /// Determines whether the complete read range is contained in the stream.
    /// </summary>
    /// <param name="stream">The stream containing the data.</param>
    /// <param name="offset">The absolute start of the range.</param>
    /// <param name="length">The number of bytes in the range.</param>
    /// <returns>Whether the range is contained in the stream.</returns>
    public static bool IsReadRangeValid(this BufferedReadStream stream, long offset, ulong length)
    {
        // Compare the offset first so subtraction cannot underflow, and avoid
        // adding an untrusted length to the offset where it could wrap around.
        ulong streamLength = (ulong)stream.Length;
        return (ulong)offset <= streamLength && length <= streamLength - (ulong)offset;
    }

    /// <summary>
    /// Gets a buffer length when the complete read fits in both the stream and an integer-sized buffer.
    /// </summary>
    /// <param name="stream">The stream containing the data.</param>
    /// <param name="length">The declared length in bytes.</param>
    /// <param name="bufferLength">The validated length, or zero when the range is invalid.</param>
    /// <returns>Whether the complete read is valid.</returns>
    public static bool TryGetReadLength(this BufferedReadStream stream, ulong length, out int bufferLength)
    {
        if (length > int.MaxValue || !stream.IsReadRangeValid(stream.Position, length))
        {
            bufferLength = 0;
            return false;
        }

        bufferLength = (int)length;
        return true;
    }

    /// <summary>
    /// Reads data from the stream into a slice of the provided buffer.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="offset">The offset within the buffer where bytes are read into.</param>
    /// <param name="count">The number of bytes, if available, to read.</param>
    /// <returns>The actual number of bytes read.</returns>
    public static int Read(this BufferedReadStream stream, Span<byte> buffer, int offset, int count)
        => stream.Read(buffer.Slice(offset, count));

    /// <summary>
    /// Advances the stream by the specified number of bytes. Nonpositive counts are ignored.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="count">The number of bytes to skip.</param>
    public static void Skip(this BufferedReadStream stream, int count)
    {
        if (count > 0)
        {
            // BufferedReadStream is always seekable; its position setter preserves
            // buffered data when the destination is inside the current buffer.
            stream.Position += count;
        }
    }
}
