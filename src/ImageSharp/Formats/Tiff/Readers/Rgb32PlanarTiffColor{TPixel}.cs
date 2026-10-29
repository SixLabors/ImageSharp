// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes RGB planes with 32-bit integer channels into Rgb96 without losing any channel bits.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb32PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb32PlanarTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        ReadOnlySpan<byte> redData = data[0].GetSpan();
        ReadOnlySpan<byte> greenData = data[1].GetSpan();
        ReadOnlySpan<byte> blueData = data[2].GetSpan();
        TiffPlanar32NativeSource source = new(this.isBigEndian == BitConverter.IsLittleEndian, 3);
        TiffRgb96Store store = default;

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            Span<Rgb96> exactRow = MemoryMarshal.Cast<TPixel, Rgb96>(pixelRow);
            int rowBytes = width * 4;

            // The factory selects this decoder only for Rgb96 output.
            // Correct byte order and combine the planes directly so all 32 channel bits survive.
            TiffNativeColorConverter<TiffPlanar32NativeSource, TiffRgb96Store, uint>.Convert(
                redData.Slice(offset, rowBytes),
                greenData.Slice(offset, rowBytes),
                blueData.Slice(offset, rowBytes),
                [],
                MemoryMarshal.Cast<Rgb96, uint>(exactRow),
                source,
                store);

            offset += rowBytes;
        }
    }
}
