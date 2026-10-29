// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.ColorConverters;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.Readers;

/// <summary>
/// Decodes RGB planes with 16 bits per channel.
/// </summary>
/// <typeparam name="TPixel">The destination pixel format.</typeparam>
internal sealed class TiffRgb48PlanarDecoder<TPixel> : TiffBasePlanarColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly Configuration configuration;
    private readonly bool reverseEndianness;

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffRgb48PlanarDecoder{TPixel}"/> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="isBigEndian">Whether stored words use big-endian byte order.</param>
    public TiffRgb48PlanarDecoder(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.reverseEndianness = isBigEndian == BitConverter.IsLittleEndian;
    }

    /// <inheritdoc />
    public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        // Each plane has one ushort per pixel. Thirty-two pixels fill one 512-bit source register per plane.
        InlineArray32<Rgb48> sourceBuffer = default;
        Span<Rgb48> samples = sourceBuffer;
        Span<Vector4> vectors = this.GetColorBuffer(this.configuration, width, []);
        ReadOnlySpan<byte> red = data[0].GetSpan();
        ReadOnlySpan<byte> green = data[1].GetSpan();
        ReadOnlySpan<byte> blue = data[2].GetSpan();
        TiffPlanar16NativeSource source = new(this.reverseEndianness, 3);
        TiffRgb48Store store = default;
        int rowOffset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            for (int x = 0; x < width; x += samples.Length)
            {
                int count = Math.Min(samples.Length, width - x);
                int byteOffset = rowOffset + (x * 2);
                int byteCount = count * 2;
                Span<Rgb48> block = samples[..count];

                // Pass the exact 16-bit channel codes to FromRgb48 so each destination format applies its integer conversion.
                TiffNativeColorConverter<TiffPlanar16NativeSource, TiffRgb48Store, ushort>.Convert(
                    red.Slice(byteOffset, byteCount),
                    green.Slice(byteOffset, byteCount),
                    blue.Slice(byteOffset, byteCount),
                    [],
                    MemoryMarshal.Cast<Rgb48, ushort>(block),
                    source,
                    store);

                if (!this.TryStoreColorPixels(this.configuration, (ReadOnlySpan<Rgb48>)block, vectors, x))
                {
                    PixelOperations<TPixel>.Instance.FromRgb48(this.configuration, block, pixelRow.Slice(x, count));
                }
            }

            this.CompleteColorRow(this.configuration, vectors, pixelRow, PixelAlphaRepresentation.None);

            rowOffset += width * 2;
        }
    }
}
