// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes RGBA planes with straight alpha and 32-bit integer channels through <see cref="Rgba128"/> pixels, so that no
/// channel bit is lost before the conversion to <typeparamref name="TPixel"/>.
/// </summary>
/// <typeparam name="TPixel">The pixel format.</typeparam>
internal class Rgba32PlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba32PlanarTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The configuration of the decode.</param>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    public Rgba32PlanarTiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
    }

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
        using IMemoryOwner<Rgba128> buffer = this.configuration.MemoryAllocator.Allocate<Rgba128>(width);
        Span<Rgba128> row = buffer.GetSpan();

        for (int y = top; y < top + height; y++)
        {
            int offset = (y - top) * rowBytes;

            // Correct the byte order and combine the planes into Rgba128 pixels, so all 32 channel bits stay. An
            // Rgba128 destination then copies the pixels.
            TiffNativeColorConverter<TiffPlanar32NativeSource, TiffRgba128Store, uint>.Convert(
                red.Slice(offset, rowBytes),
                green.Slice(offset, rowBytes),
                blue.Slice(offset, rowBytes),
                alpha.Slice(offset, rowBytes),
                MemoryMarshal.Cast<Rgba128, uint>(row),
                source,
                store);

            PixelOperations<Rgba128>.Instance.To(this.configuration, row, pixels.DangerousGetRowSpan(y).Slice(left, width));
        }
    }
}
