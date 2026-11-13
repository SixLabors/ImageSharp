// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Reads RLE commands and run data from the stream through a chunk of memory.
/// </summary>
/// <remarks>
/// <para>
/// The decoder parses each command from the chunk, so it makes no stream call for each command. A read can need more
/// bytes than the chunk holds after the current position. Then the reader moves the unread bytes to the start of the
/// chunk, and fills the rest of the chunk from the stream.
/// </para>
/// <para>
/// The reader owns the chunk. The caller disposes the reader in a <c>finally</c> block, not with a <c>using</c>
/// declaration. C# makes a <c>using</c> local read-only. A method that changes the reader, for example
/// <see cref="TryRead"/>, then runs on a copy of the struct, and the read position does not move.
/// </para>
/// </remarks>
internal ref struct BmpRleReader
{
    // The largest read is the data of an RLE24 absolute run. Its pixel count is one byte, so it has at most 255
    // pixels, and each pixel is 3 bytes (B, G, R).
    private const int MaxReadLength = 255 * 3;

    private readonly BufferedReadStream stream;
    private readonly IMemoryOwner<byte> owner;
    private readonly Span<byte> chunk;

    // The bytes chunk[position..length] are read from the stream but not consumed by the decoder.
    private int position;
    private int length;

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpRleReader"/> struct.
    /// </summary>
    /// <param name="stream">The <see cref="BufferedReadStream"/> containing image data, at the start of the RLE data.</param>
    /// <param name="allocator">The allocator of the chunk.</param>
    public BmpRleReader(BufferedReadStream stream, MemoryAllocator allocator)
    {
        // Why this chunk size:
        // BufferedReadStream.Read copies a request of BufferSize bytes or fewer through its own buffer first. A larger
        // request goes directly from the base stream into the caller's buffer, with no copy in between.
        // A fill keeps the unread bytes, fewer than MaxReadLength, at the start of the chunk, and then requests the
        // rest of the chunk. With BufferSize + MaxReadLength bytes, that request is always more than BufferSize, so
        // each fill takes the direct path. The size depends on the stream configuration only, never on the stream
        // length or on a value in the file.
        this.stream = stream;
        this.owner = allocator.Allocate<byte>(stream.BufferSize + MaxReadLength);
        this.chunk = this.owner.GetSpan();
    }

    /// <summary>
    /// Reads the next <paramref name="count"/> bytes.
    /// </summary>
    /// <remarks>
    /// Data that ends before the image is complete is not an error. The decoder keeps the pixels decoded before the
    /// end, as the PNG and JPEG decoders do, so the caller stops when this method returns <see langword="false"/>.
    /// </remarks>
    /// <param name="count">The byte count. It is at most 765, the data of an RLE24 absolute run.</param>
    /// <param name="bytes">The bytes. The span is valid until the next read.</param>
    /// <returns><see langword="true"/> if the data has <paramref name="count"/> more bytes. Else <see langword="false"/>.</returns>
    public bool TryRead(int count, out ReadOnlySpan<byte> bytes)
    {
        DebugGuard.MustBeLessThanOrEqualTo(count, MaxReadLength, nameof(count));

        if (this.length - this.position < count && !this.Fill(count))
        {
            bytes = default;
            return false;
        }

        bytes = this.chunk.Slice(this.position, count);
        this.position += count;
        return true;
    }

    /// <summary>
    /// Skips the pad byte after run data with an odd byte count.
    /// </summary>
    /// <remarks>
    /// The pad byte of the last run can be missing at the end of the stream, so a missing pad byte is not an error.
    /// </remarks>
    public void SkipPad()
    {
        if (this.length - this.position >= 1 || this.Fill(1))
        {
            this.position++;
        }
    }

    /// <summary>
    /// Moves the stream back over the bytes that the chunk holds but the decoder did not use. The stream is then
    /// after the data that the decode used.
    /// </summary>
    public readonly void Finish() => this.stream.Position -= this.length - this.position;

    /// <summary>
    /// Returns the chunk to the allocator.
    /// </summary>
    public readonly void Dispose() => this.owner.Dispose();

    /// <summary>
    /// Makes at least <paramref name="count"/> bytes available after the current position.
    /// </summary>
    /// <param name="count">The byte count. It is at most <see cref="MaxReadLength"/>.</param>
    /// <returns><see langword="true"/> if the bytes are available. <see langword="false"/> if the stream ends first.</returns>
    private bool Fill(int count)
    {
        // A fill happens only when fewer than 'count' bytes are unread, so 'unread' is less than MaxReadLength. Move
        // the unread bytes to the start of the chunk, then request the rest of the chunk. That request is more than
        // the stream buffer (see the constructor), so the stream reads it directly from the base stream. The read
        // gives at least the missing bytes unless the stream ends.
        int unread = this.length - this.position;
        this.chunk.Slice(this.position, unread).CopyTo(this.chunk);
        this.position = 0;
        this.length = unread + this.stream.ReadAtLeast(this.chunk[unread..], count - unread, false);
        return this.length >= count;
    }
}
