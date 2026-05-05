// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
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
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The number of pixels per row.</param>
    protected ExrBaseDecompressor(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
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
    /// Decompresses zip compressed data.
    /// </summary>
    /// <param name="stream">The buffered stream to decompress.</param>
    /// <param name="compressedBytes">The compressed bytes.</param>
    /// <param name="uncompressed">The buffer to write the uncompressed data to.</param>
    /// <param name="uncompressedBytes">The uncompressed bytes.</param>
    /// <returns>The total bytes read from the stream.</returns>
    protected static int UndoZipCompression(BufferedReadStream stream, uint compressedBytes, Span<byte> uncompressed, uint uncompressedBytes)
    {
        long pos = stream.Position;
        using ZlibInflateStream inflateStream = new(
                   stream,
                   () =>
                   {
                       int left = (int)(compressedBytes - (stream.Position - pos));
                       return left > 0 ? left : 0;
                   });
        inflateStream.AllocateNewBytes((int)compressedBytes, true);
        using DeflateStream dataStream = inflateStream.CompressedStream!;

        int totalRead = 0;
        while (totalRead < uncompressedBytes)
        {
            int bytesRead = dataStream.Read(uncompressed, totalRead, (int)uncompressedBytes - totalRead);
            if (bytesRead <= 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead == 0)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data for zip compressed EXR image data!");
        }

        return totalRead;
    }

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
