// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'BlackIsZero' photometric interpretation for 16-bit grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class BlackIsZero16TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlackIsZero16TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public BlackIsZero16TiffColor(Configuration configuration, bool isBigEndian)
    {
        this.configuration = configuration;
        this.isBigEndian = isBigEndian;
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        InlineArray16<L16> sourceBuffer = default;
        Span<L16> sourceBlock = sourceBuffer;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            if (this.isBigEndian)
            {
                for (int x = 0; x < width; x += sourceBlock.Length)
                {
                    int count = Math.Min(width - x, sourceBlock.Length);
                    Span<L16> samples = sourceBlock[..count];

                    // Swap encoded samples once, then let the pixel operations pack the destination row.
                    TiffUtilities.CopyUShorts(
                        data.Slice(offset + (x * 2), count * 2),
                        MemoryMarshal.Cast<L16, ushort>(samples),
                        bigEndian: true,
                        invert: false);

                    PixelOperations<TPixel>.Instance.FromL16(this.configuration, samples, pixelRow.Slice(x, count));
                }

                offset += width * 2;
            }
            else
            {
                int byteCount = pixelRow.Length * 2;
                PixelOperations<TPixel>.Instance.FromL16Bytes(
                    this.configuration,
                    data.Slice(offset, byteCount),
                    pixelRow,
                    pixelRow.Length);

                offset += byteCount;
            }
        }
    }
}
