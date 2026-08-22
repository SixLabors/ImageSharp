// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

internal sealed class DeflateCompressor : TiffBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    public DeflateCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor, DeflateCompressionLevel compressionLevel)
        : base(output, allocator, width, bitsPerPixel, predictor)
        => this.compressionLevel = compressionLevel;

    /// <inheritdoc/>
    public override TiffCompression Method => TiffCompression.Deflate;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip)
    {
    }

    /// <inheritdoc/>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        if (this.Predictor == TiffPredictor.Horizontal)
        {
            HorizontalPredictor.ApplyHorizontalPrediction(rows, this.BytesPerRow, this.BitsPerPixel);
        }

        // Compressed bytes stream straight to the output in fixed segments; the strip byte
        // count is measured by the caller from the output position.
        using ChunkedWriteStream segmentStream = new(this.Allocator, this.Output.Write);
        using ZLibStream stream = new(segmentStream, new ZLibCompressionOptions { CompressionLevel = (int)this.compressionLevel }, true);
        stream.Write(rows);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
