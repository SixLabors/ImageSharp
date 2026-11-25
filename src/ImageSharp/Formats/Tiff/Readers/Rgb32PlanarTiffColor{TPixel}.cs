// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes RGB planes with 32-bit integer channels through <see cref="Rgb96"/> pixels, so that no channel bit is lost
/// before the conversion to <typeparamref name="TPixel"/>.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb32PlanarTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration of the decode.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb32PlanarTiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        ReadOnlySpan<byte> redData = data[0].GetSpan();
        ReadOnlySpan<byte> greenData = data[1].GetSpan();
        ReadOnlySpan<byte> blueData = data[2].GetSpan();
        TiffPlanar32NativeSource source = new(this.isBigEndian == BitConverter.IsLittleEndian, 3);
        TiffRgb96Store store = default;
        using IMemoryOwner<Rgb96> buffer = this.configuration.MemoryAllocator.Allocate<Rgb96>(width);
        Span<Rgb96> exactRow = buffer.GetSpan();

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            int rowBytes = width * 4;

            // Correct the byte order and combine the planes into Rgb96 pixels, so all 32 channel bits stay. An Rgb96
            // destination then copies the pixels.
            TiffNativeColorConverter<TiffPlanar32NativeSource, TiffRgb96Store, uint>.Convert(
                redData.Slice(offset, rowBytes),
                greenData.Slice(offset, rowBytes),
                blueData.Slice(offset, rowBytes),
                [],
                MemoryMarshal.Cast<Rgb96, uint>(exactRow),
                source,
                store);

            PixelOperations<Rgb96>.Instance.To(this.configuration, exactRow, pixelRow);
            offset += rowBytes;
        }
    }
}
