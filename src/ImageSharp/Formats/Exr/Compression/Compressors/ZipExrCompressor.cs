// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

internal class ZipExrCompressor : ExrBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    private MemoryStream memoryStream = new();

    public ZipExrCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, DeflateCompressionLevel compressionLevel)
        : base(output, allocator, bytesPerBlock)
        => this.compressionLevel = compressionLevel;

    /// <inheritdoc/>
    public override ExrCompression Method => ExrCompression.Zip;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerBlock)
    {
    }

    /// <inheritdoc/>
    public override uint CompressRowBlock(Span<byte> rows, int height)
    {
        // Re-oder pixel values.
        int n = rows.Length;
        int t1 = 0;
        int t2 = (n + 1) >> 1;
        for (int i = 0; i < n; i++)
        {
            bool isOdd = (i & 1) == 1;
            rows[isOdd ? t2++ : t1++] = rows[i];
        }

        // Predictor.
        byte p = rows[0];
        for (int i = 1; i < rows.Length; i++)
        {
            int d = (rows[i] - p + 128 + 256) & 255;
            p = rows[i];
            rows[i] = (byte)d;
        }

        this.memoryStream.Seek(0, SeekOrigin.Begin);
        using (ZlibDeflateStream stream = new(this.Allocator, this.memoryStream, this.compressionLevel))
        {
            stream.Write(rows);
            stream.Flush();
        }

        int size = (int)this.memoryStream.Position;
        byte[] buffer = this.memoryStream.GetBuffer();
        this.Output.Write(buffer, 0, size);

        this.memoryStream = new();
        return (uint)size;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
