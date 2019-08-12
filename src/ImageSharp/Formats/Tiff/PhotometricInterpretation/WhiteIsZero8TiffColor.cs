// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (optimised for 8-bit grayscale images).
    /// </summary>
    internal class WhiteIsZero8TiffColor<TPixel> : TiffColorDecoder<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        public WhiteIsZero8TiffColor()
            : base(null, null)
        {
        }

        /// <summary>
        /// Decodes pixel data using the current photometric interpretation.
        /// </summary>
        /// <param name="data">The buffer to read image data from.</param>
        /// <param name="pixels">The image buffer to write pixels to.</param>
        /// <param name="left">The x-coordinate of the left-hand side of the image block.</param>
        /// <param name="top">The y-coordinate of the  top of the image block.</param>
        /// <param name="width">The width of the image block.</param>
        /// <param name="height">The height of the image block.</param>
        public override void Decode(byte[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            TPixel color = default(TPixel);

            uint offset = 0;

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> buffer = pixels.GetRowSpan(y);

                for (int x = left; x < left + width; x++)
                {
                    byte intensity = (byte)(255 - data[offset++]);
                    color.FromRgba32(new Rgba32(intensity, intensity, intensity, 255));
                    buffer[x] = color;
                }
            }
        }
    }
}
