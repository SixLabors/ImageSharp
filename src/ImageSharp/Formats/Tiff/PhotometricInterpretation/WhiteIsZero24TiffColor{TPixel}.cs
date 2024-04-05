// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation for 24-bit grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero24TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteIsZero24TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public WhiteIsZero24TiffColor(bool isBigEndian) => this.isBigEndian = isBigEndian;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        Span<byte> buffer = stackalloc byte[4];
        int bufferStartIdx = this.isBigEndian ? 1 : 0;
        const uint maxValue = 0xFFFFFF;

        Span<byte> bufferSpan = buffer[bufferStartIdx..];
        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    data.Slice(offset, 3).CopyTo(bufferSpan);
                    uint intensity = maxValue - TiffUtilities.ConvertToUIntBigEndian(buffer);
                    offset += 3;

                    pixelRow[x] = TiffUtilities.ColorScaleTo24Bit<TPixel>(intensity);
                }
            }
            else
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    data.Slice(offset, 3).CopyTo(bufferSpan);
                    uint intensity = maxValue - TiffUtilities.ConvertToUIntLittleEndian(buffer);
                    offset += 3;

                    pixelRow[x] = TiffUtilities.ColorScaleTo24Bit<TPixel>(intensity);
                }
            }
        }
    }
}
