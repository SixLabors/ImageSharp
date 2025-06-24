// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

internal sealed class DeflateCompressor : TiffBaseCompressor
{
    private readonly DeflateCompressionLevel compressionLevel;

    private readonly MemoryStream memoryStream = new MemoryStream();

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
        this.memoryStream.Seek(0, SeekOrigin.Begin);
        using (ZlibDeflateStream stream = new ZlibDeflateStream(this.Allocator, this.memoryStream, this.compressionLevel))
        {
            if (this.Predictor == TiffPredictor.Horizontal)
            {
                HorizontalPredictor.ApplyHorizontalPrediction(rows, this.BytesPerRow, this.BitsPerPixel);
            }

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
