// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Compression.Compressors;

internal class TiffJpegCompressor : TiffBaseCompressor
{
    public TiffJpegCompressor(Stream output, MemoryAllocator memoryAllocator, int width, int bitsPerPixel, TiffPredictor predictor = TiffPredictor.None)
        : base(output, memoryAllocator, width, bitsPerPixel, predictor)
    {
    }

    /// <inheritdoc/>
    public override TiffCompression Method => TiffCompression.Jpeg;

    /// <inheritdoc/>
    public override void Initialize(int rowsPerStrip)
    {
    }

    /// <inheritdoc/>
    public override void CompressStrip(Span<byte> rows, int height)
    {
        int pixelCount = rows.Length / 3;
        int width = pixelCount / height;

        using MemoryStream? memoryStream = new();
        Image<Rgb24>? image = Image.LoadPixelData<Rgb24>(rows, width, height);
        image.Save(memoryStream, new JpegEncoder()
        {
            ColorType = JpegColorType.Rgb
        });
        memoryStream.Position = 0;
        memoryStream.WriteTo(this.Output);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
    }
}
