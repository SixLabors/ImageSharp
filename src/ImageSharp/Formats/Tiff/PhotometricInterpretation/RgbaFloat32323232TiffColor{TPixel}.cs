// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with an alpha channel and with 32 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class RgbaFloat32323232TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="RgbaFloat32323232TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public RgbaFloat32323232TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        Span<byte> buffer = stackalloc byte[4];

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    data.Slice(offset, 4).CopyTo(buffer);
                    buffer.Reverse();
                    float r = BitConverter.ToSingle(buffer);
                    offset += 4;

                    data.Slice(offset, 4).CopyTo(buffer);
                    buffer.Reverse();
                    float g = BitConverter.ToSingle(buffer);
                    offset += 4;

                    data.Slice(offset, 4).CopyTo(buffer);
                    buffer.Reverse();
                    float b = BitConverter.ToSingle(buffer);
                    offset += 4;

                    data.Slice(offset, 4).CopyTo(buffer);
                    buffer.Reverse();
                    float a = BitConverter.ToSingle(buffer);
                    offset += 4;

                    pixelRow[x] = TPixel.FromScaledVector4(new Vector4(r, g, b, a));
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    float r = BitConverter.ToSingle(data.Slice(offset, 4));
                    offset += 4;

                    float g = BitConverter.ToSingle(data.Slice(offset, 4));
                    offset += 4;

                    float b = BitConverter.ToSingle(data.Slice(offset, 4));
                    offset += 4;

                    float a = BitConverter.ToSingle(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TPixel.FromScaledVector4(new Vector4(r, g, b, a));
                }
            }
        }
    }
}
