// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimized for 8-bit grayscale images).
    /// </summary>
    internal class WhiteIsZero8TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);

            int offset = 0;

            var l8 = default(L8);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    byte intensity = (byte)(byte.MaxValue - data[offset++]);
                    pixelRow[x] = TiffUtils.ColorFromL8(l8, intensity, color);
                }
            }
        }
    }
}
