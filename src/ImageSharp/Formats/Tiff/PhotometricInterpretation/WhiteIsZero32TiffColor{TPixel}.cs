// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation for 32-bit grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero32TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteIsZero32TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public WhiteIsZero32TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        const uint maxValue = 0xFFFFFFFF;

        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint intensity = maxValue - TiffUtilities.ConvertToUIntBigEndian(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(intensity);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    uint intensity = maxValue - TiffUtilities.ConvertToUIntLittleEndian(data.Slice(offset, 4));
                    offset += 4;

                    pixelRow[x] = TiffUtilities.ColorScaleTo32Bit<TPixel>(intensity);
                }
            }
        }
    }
}
