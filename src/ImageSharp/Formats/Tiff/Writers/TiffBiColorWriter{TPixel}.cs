// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Formats.Tiff.Writers;

internal sealed class TiffBiColorWriter<TPixel> : TiffBaseColorWriter<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Image<TPixel> imageBlackWhite;

    private IMemoryOwner<byte> pixelsAsGray;

    private IMemoryOwner<byte> bitStrip;

    public TiffBiColorWriter(
        ImageFrame<TPixel> image,
        Size encodingSize,
        MemoryAllocator memoryAllocator,
        Configuration configuration,
        TiffEncoderEntriesCollector entriesCollector)
        : base(image, encodingSize, memoryAllocator, configuration, entriesCollector)
    {
        // Convert image to black and white.
        this.imageBlackWhite = new Image<TPixel>(configuration, new ImageMetadata(), [image.Clone()]);
        this.imageBlackWhite.Mutate(img => img.BinaryDither(KnownDitherings.FloydSteinberg));
    }

    /// <inheritdoc/>
    public override int BitsPerPixel => 1;

    /// <inheritdoc/>
    protected override void EncodeStrip(int y, int height, TiffBaseCompressor compressor)
    {
        int width = this.Width;

        if (compressor.Method is TiffCompression.CcittGroup3Fax or TiffCompression.Ccitt1D or TiffCompression.CcittGroup4Fax)
        {
            // Special case for T4BitCompressor.
            int stripPixels = width * height;
            this.pixelsAsGray ??= this.MemoryAllocator.Allocate<byte>(stripPixels);
            this.imageBlackWhite.ProcessPixelRows(accessor =>
            {
                Span<byte> pixelAsGraySpan = this.pixelsAsGray.GetSpan();
                int lastRow = y + height;
                int grayRowIdx = 0;
                for (int row = y; row < lastRow; row++)
                {
                    Span<TPixel> pixelsBlackWhiteRow = accessor.GetRowSpan(row);
                    Span<byte> pixelAsGrayRow = pixelAsGraySpan.Slice(grayRowIdx * width, width);
                    PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelsBlackWhiteRow, pixelAsGrayRow, width);
                    grayRowIdx++;
                }

                compressor.CompressStrip(pixelAsGraySpan[..stripPixels], height);
            });
        }
        else
        {
            // Write uncompressed image.
            int bytesPerStrip = this.BytesPerRow * height;
            this.bitStrip ??= this.MemoryAllocator.Allocate<byte>(bytesPerStrip);
            this.pixelsAsGray ??= this.MemoryAllocator.Allocate<byte>(width);
            Span<byte> pixelAsGraySpan = this.pixelsAsGray.GetSpan();

            Span<byte> rows = this.bitStrip.Slice(0, bytesPerStrip);
            rows.Clear();
            Buffer2D<TPixel> blackWhiteBuffer = this.imageBlackWhite.Frames.RootFrame.PixelBuffer;

            int outputRowIdx = 0;
            int lastRow = y + height;
            for (int row = y; row < lastRow; row++)
            {
                int bitIndex = 0;
                int byteIndex = 0;
                Span<byte> outputRow = rows[(outputRowIdx * this.BytesPerRow)..];
                Span<TPixel> pixelsBlackWhiteRow = blackWhiteBuffer.DangerousGetRowSpan(row)[..width];
                PixelOperations<TPixel>.Instance.ToL8Bytes(this.Configuration, pixelsBlackWhiteRow, pixelAsGraySpan, width);
                for (int x = 0; x < this.Width; x++)
                {
                    int shift = 7 - bitIndex;
                    if (pixelAsGraySpan[x] == 255)
                    {
                        outputRow[byteIndex] |= (byte)(1 << shift);
                    }

                    bitIndex++;
                    if (bitIndex == 8)
                    {
                        byteIndex++;
                        bitIndex = 0;
                    }
                }

                outputRowIdx++;
            }

            compressor.CompressStrip(rows, height);
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.imageBlackWhite?.Dispose();
        this.pixelsAsGray?.Dispose();
        this.bitStrip?.Dispose();
    }
}
