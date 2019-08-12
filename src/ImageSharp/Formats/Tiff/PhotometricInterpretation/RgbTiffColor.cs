// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements the 'RGB' photometric interpretation (for all bit depths).
    /// </summary>
    internal class RgbTiffColor<TPixel> : TiffColorDecoder<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private readonly float rFactor;

        private readonly float gFactor;

        private readonly float bFactor;

        private readonly uint bitsPerSampleR;

        private readonly uint bitsPerSampleG;

        private readonly uint bitsPerSampleB;

        public RgbTiffColor(uint[] bitsPerSample)
          : base(bitsPerSample, null)
        {
            this.rFactor = (float)(1 << (int)bitsPerSample[0]) - 1.0f;
            this.gFactor = (float)(1 << (int)bitsPerSample[1]) - 1.0f;
            this.bFactor = (float)(1 << (int)bitsPerSample[2]) - 1.0f;

            this.bitsPerSampleR = bitsPerSample[0];
            this.bitsPerSampleG = bitsPerSample[1];
            this.bitsPerSampleB = bitsPerSample[2];
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
                    float r = ((float)bitReader.ReadBits(this.bitsPerSampleR)) / this.rFactor;
                    float g = ((float)bitReader.ReadBits(this.bitsPerSampleG)) / this.gFactor;
                    float b = ((float)bitReader.ReadBits(this.bitsPerSampleB)) / this.bFactor;
                    color.FromVector4(new Vector4(r, g, b, 1.0f));

                    buffer[x] = color;
                }

                bitReader.NextRow();
            }
        }
    }
}
