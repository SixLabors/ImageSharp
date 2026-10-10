// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    public static void Write(this Stream stream, ReadOnlySpan<byte> buffer, int offset, int count)
        => stream.Write(buffer.Slice(offset, count));
}
