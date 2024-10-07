// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'YCbCr'.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class YCbCrTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly MemoryAllocator memoryAllocator;

    private readonly YCbCrConverter converter;

    private readonly ushort[] ycbcrSubSampling;

    public YCbCrTiffColor(MemoryAllocator memoryAllocator, Rational[] referenceBlackAndWhite, Rational[] coefficients, ushort[] ycbcrSubSampling)
    {
        this.memoryAllocator = memoryAllocator;
        this.converter = new(referenceBlackAndWhite, coefficients);
        this.ycbcrSubSampling = ycbcrSubSampling;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        ReadOnlySpan<byte> ycbcrData = data;
        if (this.ycbcrSubSampling != null && !(this.ycbcrSubSampling[0] == 1 && this.ycbcrSubSampling[1] == 1))
        {
            // 4 extra rows and columns for possible padding.
            int paddedWidth = width + 4;
            int paddedHeight = height + 4;
            int requiredBytes = paddedWidth * paddedHeight * 3;
            using IMemoryOwner<byte> tmpBuffer = this.memoryAllocator.Allocate<byte>(requiredBytes);
            Span<byte> tmpBufferSpan = tmpBuffer.GetSpan();
            ReverseChromaSubSampling(width, height, this.ycbcrSubSampling[0], this.ycbcrSubSampling[1], data, tmpBufferSpan);
            ycbcrData = tmpBufferSpan;
            this.DecodeYCbCrData(pixels, left, top, width, height, ycbcrData);
            return;
        }

        this.DecodeYCbCrData(pixels, left, top, width, height, ycbcrData);
    }

    private void DecodeYCbCrData(Buffer2D<TPixel> pixels, int left, int top, int width, int height, ReadOnlySpan<byte> ycbcrData)
    {
        int offset = 0;
        int widthPadding = 0;
        if (this.ycbcrSubSampling != null)
        {
            // Round to the next integer multiple of horizontalSubSampling.
            widthPadding = TiffUtilities.PaddingToNextInteger(width, this.ycbcrSubSampling[0]);
        }

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                Rgba32 rgba = this.converter.ConvertToRgba32(ycbcrData[offset], ycbcrData[offset + 1], ycbcrData[offset + 2]);
                pixelRow[x] = TPixel.FromRgba32(rgba);
                offset += 3;
            }

            offset += widthPadding * 3;
        }
    }

    private static void ReverseChromaSubSampling(int width, int height, int horizontalSubSampling, int verticalSubSampling, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        // If width and height are not multiples of ChromaSubsampleHoriz and ChromaSubsampleVert respectively,
        // then the source data will be padded.
        width += TiffUtilities.PaddingToNextInteger(width, horizontalSubSampling);
        height += TiffUtilities.PaddingToNextInteger(height, verticalSubSampling);
        int blockWidth = width / horizontalSubSampling;
        int blockHeight = height / verticalSubSampling;
        int cbCrOffsetInBlock = horizontalSubSampling * verticalSubSampling;
        int blockByteCount = cbCrOffsetInBlock + 2;

        for (int blockRow = blockHeight - 1; blockRow >= 0; blockRow--)
        {
            for (int blockCol = blockWidth - 1; blockCol >= 0; blockCol--)
            {
                int blockOffset = (blockRow * blockWidth) + blockCol;
                ReadOnlySpan<byte> blockData = source.Slice(blockOffset * blockByteCount, blockByteCount);
                byte cr = blockData[cbCrOffsetInBlock + 1];
                byte cb = blockData[cbCrOffsetInBlock];

                for (int row = verticalSubSampling - 1; row >= 0; row--)
                {
                    for (int col = horizontalSubSampling - 1; col >= 0; col--)
                    {
                        int offset = 3 * ((((blockRow * verticalSubSampling) + row) * width) + (blockCol * horizontalSubSampling) + col);
                        destination[offset + 2] = cr;
                        destination[offset + 1] = cb;
                        destination[offset] = blockData[(row * horizontalSubSampling) + col];
                    }
                }
            }
        }
    }
}
