// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Decodes RGBA planes with 32-bit integer channels into Rgba128 without losing any channel bits.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class Rgba32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba32PlanarTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    public Rgba32PlanarTiffColor(bool isBigEndian) => this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;

    /// <inheritdoc />
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        ReadOnlySpan<byte> red = data[0].GetSpan();
        ReadOnlySpan<byte> green = data[1].GetSpan();
        ReadOnlySpan<byte> blue = data[2].GetSpan();
        ReadOnlySpan<byte> alpha = data[3].GetSpan();
        TiffPlanar32NativeSource source = new(this.reverseEndianness, 4);
        TiffRgba128Store store = default;
        int rowBytes = width * sizeof(uint);

        for (int y = top; y < top + height; y++)
        {
            int offset = (y - top) * rowBytes;
            Span<Rgba128> row = MemoryMarshal.Cast<TPixel, Rgba128>(pixels.DangerousGetRowSpan(y).Slice(left, width));
            TiffNativeColorConverter<TiffPlanar32NativeSource, TiffRgba128Store, uint>.Convert(red.Slice(offset, rowBytes), green.Slice(offset, rowBytes), blue.Slice(offset, rowBytes), alpha.Slice(offset, rowBytes), MemoryMarshal.Cast<Rgba128, uint>(row), source, store);
        }
    }
}
