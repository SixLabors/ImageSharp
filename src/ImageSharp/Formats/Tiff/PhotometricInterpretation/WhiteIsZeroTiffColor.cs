// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'WhiteIsZero' photometric interpretation (for all bit depths).
    /// </summary>
    internal class WhiteIsZeroTiffColor<TPixel> : TiffColorDecoder<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly uint bitsPerSample0;

        private readonly float factor;

        public WhiteIsZeroTiffColor(uint[] bitsPerSample)
            : base(bitsPerSample, null)
        {
            this.bitsPerSample0 = bitsPerSample[0];
            this.factor = (float)(1 << (int)this.bitsPerSample0) - 1.0f;
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

            BitReader bitReader = new BitReader(data);

            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> buffer = pixels.GetRowSpan(y);

                for (int x = left; x < left + width; x++)
                {
                    int value = bitReader.ReadBits(this.bitsPerSample0);
                    float intensity = 1.0f - (((float)value) / this.factor);
                    color.FromVector4(new Vector4(intensity, intensity, intensity, 1.0f));
                    buffer[x] = color;
                }

                bitReader.NextRow();
            }
        }
    }
}
