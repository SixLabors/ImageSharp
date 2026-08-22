// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

/// <summary>
/// Compressor for EXR image data using the ZIP compression.
/// </summary>
internal class ZipExrCompressor : ExrBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    private readonly System.Buffers.IMemoryOwner<byte> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipExrCompressor"/> class.
    /// </summary>
    /// <param name="output">The stream to write the compressed data to.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="compressionLevel">The compression level for deflate compression.</param>
    public ZipExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width, DeflateCompressionLevel compressionLevel)
        : base(output, allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width)
    {
        this.compressionLevel = compressionLevel;
        this.buffer = allocator.Allocate<byte>((int)bytesPerBlock);
    }

    /// <inheritdoc/>
    public override uint CompressRowBlock(Span<byte> rows, int rowCount)
    {
        // Re-oder pixel values.
        Span<byte> reordered = this.buffer.GetSpan()[..(int)(rowCount * this.BytesPerRow)];
        int n = reordered.Length;
        int t1 = 0;
        int t2 = (n + 1) >> 1;
        for (int i = 0; i < n; i++)
        {
            bool isOdd = (i & 1) == 1;
            reordered[isOdd ? t2++ : t1++] = rows[i];
        }

        // Predictor.
        Span<byte> predicted = reordered;
        byte p = predicted[0];
        for (int i = 1; i < predicted.Length; i++)
        {
            int d = (predicted[i] - p + 128 + 256) & 255;
            p = predicted[i];
            predicted[i] = (byte)d;
        }

        // Compressed bytes stream straight to the output in fixed segments. The block size is
        // totaled in the callback because the final partial segment is only emitted on disposal.
        uint size = 0;
        using (ChunkedWriteStream segmentStream = new(this.Allocator, segment =>
        {
            this.Output.Write(segment);
            size += (uint)segment.Length;
        }))
        using (ZLibStream stream = new(segmentStream, new ZLibCompressionOptions { CompressionLevel = (int)this.compressionLevel }, true))
        {
            stream.Write(predicted);
        }

        return size;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.buffer.Dispose();
}
