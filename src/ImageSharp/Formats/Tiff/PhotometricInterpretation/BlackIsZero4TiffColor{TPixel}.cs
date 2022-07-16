// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'BlackIsZero' photometric interpretation (optimized for 4-bit grayscale images).
    /// </summary>
    internal class BlackIsZero4TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;
            bool isOddWidth = (width & 1) == 1;

            var l8 = default(L8);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan(y);
                for (int x = left; x < left + width - 1;)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)(((byteData & 0xF0) >> 4) * 17);
                    l8.PackedValue = intensity1;
                    color.FromL8(l8);

                    pixelRowSpan[x++] = color;

                    byte intensity2 = (byte)((byteData & 0x0F) * 17);
                    l8.PackedValue = intensity2;
                    color.FromL8(l8);

                    pixelRowSpan[x++] = color;
                }

                if (isOddWidth)
                {
                    byte byteData = data[offset++];

                    byte intensity1 = (byte)(((byteData & 0xF0) >> 4) * 17);
                    l8.PackedValue = intensity1;
                    color.FromL8(l8);

                    pixelRowSpan[left + width - 1] = color;
                }
            }
        }
    }
}
