// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
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
        InlineArray16<Rgb48> sourceBuffer = default;
        Span<Rgb48> sourceBlock = sourceBuffer;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

            if (this.isBigEndian)
            {
                for (int x = 0; x < width; x += sourceBlock.Length)
                {
                    int count = Math.Min(width - x, sourceBlock.Length);
                    Span<Rgb48> samples = sourceBlock[..count];

                    // Six stored bytes per pixel become three native-endian ushort lanes for bulk packing.
                    TiffUtilities.CopyUShorts(
                        data.Slice(offset + (x * 6), count * 6),
                        MemoryMarshal.Cast<Rgb48, ushort>(samples),
                        bigEndian: true,
                        invert: false);

                    PixelOperations<TPixel>.Instance.FromRgb48(this.configuration, samples, pixelRow.Slice(x, count));
                }

                offset += width * 6;
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
