// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimized for bilevel images).
    /// </summary>
    internal class WhiteIsZero1TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            int offset = 0;
            var colorBlack = default(TPixel);
            var colorWhite = default(TPixel);

            colorBlack.FromRgba32(Color.Black);
            colorWhite.FromRgba32(Color.White);
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRowSpan = pixels.DangerousGetRowSpan(y);
                for (int x = left; x < left + width; x += 8)
                {
                    byte b = data[offset++];
                    int maxShift = Math.Min(left + width - x, 8);

                    for (int shift = 0; shift < maxShift; shift++)
                    {
                        int bit = (b >> (7 - shift)) & 1;

                        pixelRowSpan[x + shift] = bit == 0 ? colorWhite : colorBlack;
                    }
                }
            }
        }
    }
}
