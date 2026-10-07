// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation for 16-bit grayscale images.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero16TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly bool isBigEndian;

    private readonly Configuration configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhiteIsZero16TiffColor{TPixel}" /> class.
    /// </summary>
    /// <param name="configuration">The image configuration.</param>
    /// <param name="isBigEndian">if set to <c>true</c> decodes the pixel data as big endian, otherwise as little endian.</param>
    public WhiteIsZero16TiffColor(Configuration configuration, bool isBigEndian)
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
            for (int x = 0; x < width; x += sourceBlock.Length)
            {
                int count = Math.Min(width - x, sourceBlock.Length);
                Span<L16> samples = sourceBlock[..count];

                // Decode byte order and white-is-zero inversion in the same source traversal.
                TiffUtilities.CopyUShorts(data.Slice(offset + (x * 2), count * 2), MemoryMarshal.Cast<L16, ushort>(samples), this.isBigEndian, invert: true);
                PixelOperations<TPixel>.Instance.FromL16(this.configuration, samples, pixelRow.Slice(x, count));
            }

            offset += width * 2;
        }
    }
}
