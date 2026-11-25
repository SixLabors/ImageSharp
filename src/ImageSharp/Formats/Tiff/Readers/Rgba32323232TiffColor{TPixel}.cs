// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes packed RGBA with straight alpha and 32-bit integer channels through <see cref="Rgba128"/> pixels, so that
/// no channel bit is lost before the conversion to <typeparamref name="TPixel"/>.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgba32323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgba32323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration of the decode.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgba32323232TiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        PixelOperations<Rgba128> operations = PixelOperations<Rgba128>.Instance;
        int rowBytes = width * 16;
        int offset = 0;

        // When the stored byte order is the native byte order, the stored words are Rgba128 pixels already, and the
        // conversion reads them in place. Else each word is reversed into a row buffer first.
        bool reverse = this.isBigEndian == BitConverter.IsLittleEndian;
        using IMemoryOwner<Rgba128>? buffer = reverse ? this.configuration.MemoryAllocator.Allocate<Rgba128>(width) : null;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            ReadOnlySpan<byte> encoded = data.Slice(offset, rowBytes);

            // An Rgba128 destination copies the words, so all 32 bits of each channel stay.
            if (buffer is null)
            {
                operations.To(this.configuration, MemoryMarshal.Cast<byte, Rgba128>(encoded), pixelRow);
            }
            else
            {
                Span<Rgba128> row = buffer.GetSpan();
                BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, uint>(encoded), MemoryMarshal.Cast<Rgba128, uint>(row));
                operations.To(this.configuration, row, pixelRow);
            }

            offset += rowBytes;
        }
    }
}
