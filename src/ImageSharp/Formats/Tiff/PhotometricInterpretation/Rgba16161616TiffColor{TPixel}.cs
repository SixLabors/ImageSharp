// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#nullable disable

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 16 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba16161616TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    private readonly Configuration configuration;

    private readonly MemoryAllocator memoryAllocator;

    private readonly TiffExtraSampleType? extraSamplesType;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba16161616TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="memoryAllocator">The memory allocator.</param>
    /// <param name="extraSamplesType">The type of the extra samples.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgba16161616TiffColor(Configuration configuration, MemoryAllocator memoryAllocator, TiffExtraSampleType? extraSamplesType, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
        this.memoryAllocator = memoryAllocator;
        this.extraSamplesType = extraSamplesType;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
        int offset = 0;

        using IMemoryOwner<Vector4> vectors = hasAssociatedAlpha ? this.memoryAllocator.Allocate<Vector4>(width) : null;
        Span<Vector4> vectorsSpan = hasAssociatedAlpha ? vectors.GetSpan() : [];

        if (this.isBigEndian)
        {
            if (hasAssociatedAlpha)
            {
                for (int y = top; y < top + height; y++)
                {
                    Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ushort r = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        ushort g = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 2, 2));
                        ushort b = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 4, 2));
                        ushort a = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 6, 2));
                        offset += 8;

                        pixelRow[x] = TiffUtilities.ColorFromRgba64Premultiplied<TPixel>(r, g, b, a);
                    }
                }
            }
            else
            {
                for (int y = top; y < top + height; y++)
                {
                    Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                    for (int x = 0; x < pixelRow.Length; x++)
                    {
                        ushort r = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                        ushort g = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 2, 2));
                        ushort b = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 4, 2));
                        ushort a = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset + 6, 2));
                        offset += 8;

                        pixelRow[x] = TPixel.FromRgba64(new Rgba64(r, g, b, a));
                    }
                }
            }
        }
        else
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                int byteCount = pixelRow.Length * 8;

                if (hasAssociatedAlpha)
                {
                    // This little-endian branch can view the interleaved samples as Rgba64. The values are already associated, so
                    // the Rgba64 operation performs only unpacking and normalization before the destination consumes that representation.
                    ReadOnlySpan<Rgba64> associatedSamples = MemoryMarshal.Cast<byte, Rgba64>(data.Slice(offset, byteCount));
                    PixelOperations<Rgba64>.Instance.ToVector4(this.configuration, associatedSamples, vectorsSpan, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);
                    PixelOperations<TPixel>.Instance.FromVector4Destructive(this.configuration, vectorsSpan, pixelRow, PixelConversionModifiers.Scale | PixelConversionModifiers.Premultiply);
                }
                else
                {
                    PixelOperations<TPixel>.Instance.FromRgba64Bytes(
                        this.configuration,
                        data.Slice(offset, byteCount),
                        pixelRow,
                        pixelRow.Length);
                }

                offset += byteCount;
            }
        }
    }
}
