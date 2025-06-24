// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

internal sealed class LzwCompressor : TiffBaseCompressor
{
    private TiffLzwEncoder lzwEncoder;

    public LzwCompressor(Stream output, MemoryAllocator allocator, int width, int bitsPerPixel, TiffPredictor predictor)
        : base(output, allocator, width, bitsPerPixel, predictor)
    {
    }

    /// <inheritdoc/>
    public override TiffCompression Method => TiffCompression.Lzw;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip) => this.lzwEncoder = new(this.Allocator);

    /// <inheritdoc/>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        if (this.Predictor == TiffPredictor.Horizontal)
        {
            HorizontalPredictor.ApplyHorizontalPrediction(rows, this.BytesPerRow, this.BitsPerPixel);
        }

        this.lzwEncoder.Encode(rows, this.Output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.lzwEncoder?.Dispose();
}
