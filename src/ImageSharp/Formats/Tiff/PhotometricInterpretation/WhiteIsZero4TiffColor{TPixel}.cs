// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'WhiteIsZero' photometric interpretation (optimized for 4-bit grayscale images).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class WhiteIsZero4TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        bool isOddWidth = (width & 1) == 1;

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan(y);
            for (int x = left; x < left + width - 1;)
            {
                byte byteData = data[offset++];
                pixelRowSpan[x++] = TPixel.FromL8(new L8((byte)((15 - ((byteData & 0xF0) >> 4)) * 17)));
                pixelRowSpan[x++] = TPixel.FromL8(new L8((byte)((15 - (byteData & 0x0F)) * 17)));
            }

            if (isOddWidth)
            {
                byte byteData = data[offset++];
                pixelRowSpan[left + width - 1] = TPixel.FromL8(new L8((byte)((15 - ((byteData & 0xF0) >> 4)) * 17)));
            }
        }
    }
}
