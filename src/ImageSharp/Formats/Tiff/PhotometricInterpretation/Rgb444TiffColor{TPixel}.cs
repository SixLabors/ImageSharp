// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation for 4 bits per color channel images.
    /// </summary>
    internal class Rgb444TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var bgra = default(Bgra4444);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y);

                for (int x = left; x < left + width; x += 2)
                {
                    byte r = (byte)((data[offset] & 0xF0) >> 4);
                    byte g = (byte)(data[offset] & 0xF);
                    offset++;
                    byte b = (byte)((data[offset] & 0xF0) >> 4);

                    bgra.PackedValue = ToBgraPackedValue(b, g, r);
                    color.FromScaledVector4(bgra.ToScaledVector4());
                    pixelRow[x] = color;
                    if (x + 1 >= pixelRow.Length)
                    {
                        offset++;
                        break;
                    }

                    r = (byte)(data[offset] & 0xF);
                    offset++;
                    g = (byte)((data[offset] & 0xF0) >> 4);
                    b = (byte)(data[offset] & 0xF);
                    offset++;

                    bgra.PackedValue = ToBgraPackedValue(b, g, r);
                    color.FromScaledVector4(bgra.ToScaledVector4());
                    pixelRow[x + 1] = color;
                }
            }
        }

        private static ushort ToBgraPackedValue(byte b, byte g, byte r) => (ushort)(b | (g << 4) | (r << 8) | (0xF << 12));
    }
}
