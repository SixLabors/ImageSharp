// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation for 32-bit float grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero32FloatTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteIsZero32FloatTiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public WhiteIsZero32FloatTiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> buffer = stackalloc byte[4];

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    data.Slice(offset, 4).CopyTo(buffer);
                    buffer.Reverse();
                    float intensity = 1.0f - BitConverter.ToSingle(buffer);
                    offset += 4;

                    pixelRow[x] = TPixel.FromScaledVector4(new(intensity, intensity, intensity, 1f));
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    float intensity = 1.0f - BitConverter.ToSingle(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TPixel.FromScaledVector4(new(intensity, intensity, intensity, 1.0f));
                }
            }
        }
    }
}
