// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// The base EXR decompressor class.
/// </summary>
internal abstract class ExrBaseDecompressor : ExrBaseCompression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrBaseDecompressor" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per row block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    protected ExrBaseDecompressor(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
        : base(allocator, bytesPerBlock, bytesPerRow)
    {
    }

    /// <summary>
    /// Decompresses the specified stream.
    /// </summary>
    /// <param name="stream">The buffered stream to decompress.</param>
    /// <param name="compressedBytes">The compressed bytes.</param>
    /// <param name="buffer">The buffer to write the decompressed data to.</param>
    public abstract void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer);

    /// <summary>
    /// Integrate over all differences to the previous value in order to
    /// reconstruct sample values.
    /// </summary>
    /// <param name="buffer">The buffer with the data.</param>
    /// <param name="unCompressedBytes">The un compressed bytes.</param>
    protected static void Reconstruct(Span<byte> buffer, uint unCompressedBytes)
    {
        int offset = 0;
        for (int i = 0; i < unCompressedBytes - 1; i++)
        {
            byte d = (byte)(buffer[offset] + (buffer[offset + 1] - 128));
            buffer[offset + 1] = d;
            offset++;
        }
    }

    /// <summary>
    /// Interleaves the input data.
    /// </summary>
    /// <param name="source">The source data.</param>
    /// <param name="unCompressedBytes">The uncompressed bytes.</param>
    /// <param name="output">The output to write to.</param>
    protected static void Interleave(Span<byte> source, uint unCompressedBytes, Span<byte> output)
    {
        int sourceOffset = 0;
        int offset0 = 0;
        int offset1 = (int)((unCompressedBytes + 1) / 2);
        while (sourceOffset < unCompressedBytes)
        {
            output[sourceOffset++] = source[offset0++];
            output[sourceOffset++] = source[offset1++];
        }
    }
}
