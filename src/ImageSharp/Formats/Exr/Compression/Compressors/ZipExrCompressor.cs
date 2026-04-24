// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

/// <summary>
/// Compressor for EXR image data using the ZIP compression.
/// </summary>
internal class ZipExrCompressor : ExrBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    private readonly MemoryStream memoryStream;

    private readonly System.Buffers.IMemoryOwner<byte> buffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipExrCompressor"/> class.
    /// </summary>
    /// <param name="output">The stream to write the compressed data to.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="compressionLevel">The compression level for deflate compression.</param>
    public ZipExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, int width, DeflateCompressionLevel compressionLevel)
        : base(output, allocator, bytesPerBlock, bytesPerRow, width)
    {
        this.compressionLevel = compressionLevel;
        this.buffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.memoryStream = new();
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

        this.memoryStream.Seek(0, SeekOrigin.Begin);
        using (ZlibDeflateStream stream = new(this.Allocator, this.memoryStream, this.compressionLevel))
        {
            stream.Write(predicted);
            stream.Flush();
        }

        int size = (int)this.memoryStream.Position;
        byte[] buffer = this.memoryStream.GetBuffer();
        this.Output.Write(buffer, 0, size);

        // Reset memory stream for next pixel row.
        this.memoryStream.Seek(0, SeekOrigin.Begin);
        this.memoryStream.SetLength(0);

        return (uint)size;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.buffer.Dispose();
        this.memoryStream?.Dispose();
    }
}
