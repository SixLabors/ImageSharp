// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Compressors;

internal class ZipExrCompressor : ExrBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    private readonly MemoryStream memoryStream = new();

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
    public override void CompressStrip(Span<byte> rows, int height)
    {
        this.memoryStream.Seek(0, SeekOrigin.Begin);
        using (ZlibDeflateStream stream = new(this.Allocator, this.memoryStream, this.compressionLevel))
        {
            stream.Write(rows);
            stream.Flush();
        }

        int size = (int)this.memoryStream.Position;
        byte[] buffer = this.memoryStream.GetBuffer();
        this.Output.Write(buffer, 0, size);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
