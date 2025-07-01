// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'RGB' photometric interpretation with 16 bits for each channel.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class Rgb161616TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;
    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rgb161616TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public Rgb161616TiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            if (this.isBigEndian)
            {
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    ushort r = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                    offset += 2;
                    ushort g = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                    offset += 2;
                    ushort b = TiffUtilities.ConvertToUShortBigEndian(data.Slice(offset, 2));
                    offset += 2;

                    pixelRow[x] = TPixel.FromRgb48(new Rgb48(r, g, b));
                }
            }
            else
            {
                int byteCount = pixelRow.Length * 6;
                PixelOperations<TPixel>.Instance.FromRgb48Bytes(
                    this.configuration,
                    data.Slice(offset, byteCount),
                    pixelRow,
                    pixelRow.Length);

                offset += byteCount;
            }
        }
    }
}
