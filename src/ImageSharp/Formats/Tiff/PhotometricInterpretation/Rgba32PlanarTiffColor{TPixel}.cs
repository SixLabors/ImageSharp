// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and a 'Planar' layout for each color channel with 32 bit.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;
    private readonly TiffExtraSampleType? extraSamplesType;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba32PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="extraSamplesType">The extra samples type.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgba32PlanarTiffColor(TiffExtraSampleType? extraSamplesType, bool isBigEndian)
    {
        this.extraSamplesType = extraSamplesType;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> redData = data[0].GetSpan();
        Span<byte> greenData = data[1].GetSpan();
        Span<byte> blueData = data[2].GetSpan();
        Span<byte> alphaData = data[3].GetSpan();

        bool hasAssociatedAlpha = this.extraSamplesType.HasValue && this.extraSamplesType == TiffExtraSampleType.AssociatedAlphaData;
        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (!hasAssociatedAlpha && typeof(TPixel) == typeof(Rgba128))
            {
                // Straight integer pixels retain the stored samples. Associated
                // alpha still needs the generic alpha conversion below.
                Span<Rgba128> exactRow = MemoryMarshal.Cast<TPixel, Rgba128>(pixelRow);
                ReadOnlySpan<uint> first = MemoryMarshal.Cast<byte, uint>(redData.Slice(offset, width * 4));
                ReadOnlySpan<uint> second = MemoryMarshal.Cast<byte, uint>(greenData.Slice(offset, width * 4));
                ReadOnlySpan<uint> third = MemoryMarshal.Cast<byte, uint>(blueData.Slice(offset, width * 4));
                ReadOnlySpan<uint> fourth = MemoryMarshal.Cast<byte, uint>(alphaData.Slice(offset, width * 4));

                if (this.isBigEndian != BitConverter.IsLittleEndian)
                {
                    for (int x = 0; x < exactRow.Length; x++)
                    {
                        exactRow[x] = new Rgba128(first[x], second[x], third[x], fourth[x]);
                    }
                }
                else
                {
                    for (int x = 0; x < exactRow.Length; x++)
                    {
                        exactRow[x] = new Rgba128(
                            BinaryPrimitives.ReverseEndianness(first[x]),
                            BinaryPrimitives.ReverseEndianness(second[x]),
                            BinaryPrimitives.ReverseEndianness(third[x]),
                            BinaryPrimitives.ReverseEndianness(fourth[x]));
                    }
                }

                offset += width * 4;
                continue;
            }

            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntBigEndian(redData.Slice(offset, 4));
                    uint g = TiffUtilities.ConvertToUIntBigEndian(greenData.Slice(offset, 4));
                    uint b = TiffUtilities.ConvertToUIntBigEndian(blueData.Slice(offset, 4));
                    uint a = TiffUtilities.ConvertToUIntBigEndian(alphaData.Slice(offset, 4));

                    offset += 4;

                    pixelRow[x] = hasAssociatedAlpha
                        ? TiffUtilities.ColorScaleTo32BitPremultiplied<TPixel>(r, g, b, a)
                        : TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b, a);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint r = TiffUtilities.ConvertToUIntLittleEndian(redData.Slice(offset, 4));
                    uint g = TiffUtilities.ConvertToUIntLittleEndian(greenData.Slice(offset, 4));
                    uint b = TiffUtilities.ConvertToUIntLittleEndian(blueData.Slice(offset, 4));
                    uint a = TiffUtilities.ConvertToUIntLittleEndian(alphaData.Slice(offset, 4));

                    offset += 4;

                    pixelRow[x] = hasAssociatedAlpha
                        ? TiffUtilities.ColorScaleTo32BitPremultiplied<TPixel>(r, g, b, a)
                        : TiffUtilities.ColorScaleTo32Bit<TPixel>(r, g, b, a);
                }
            }
        }
    }
}
