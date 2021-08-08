// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    internal class YCbCrTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly YCbCrConverter converter;

        private static readonly Rational[] DefaultLuma =
        {
            new Rational(299, 1000),
            new Rational(587, 1000),
            new Rational(114, 1000)
        };

        private static readonly Rational[] DefaultReferenceBlackWhite =
        {
            new Rational(0, 1), new Rational(255, 1),
            new Rational(128, 1), new Rational(255, 1),
            new Rational(128, 1), new Rational(255, 1)
        };

        public YCbCrTiffColor(Rational[] referenceBlackAndWhite, Rational[] coefficients)
        {
            referenceBlackAndWhite ??= DefaultReferenceBlackWhite;
            coefficients ??= DefaultLuma;

            if (referenceBlackAndWhite.Length != 6)
            {
                TiffThrowHelper.ThrowImageFormatException("reference black and white array should have 6 entry's");
            }

            if (coefficients.Length != 3)
            {
                TiffThrowHelper.ThrowImageFormatException("luma coefficients array should have 6 entry's");
            }

            this.converter = new YCbCrConverter(referenceBlackAndWhite, coefficients);
        }

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);
            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    Rgba32 rgba = this.converter.ConvertToRgba32(data[offset], data[offset + 1], data[offset + 2]);
                    color.FromRgba32(rgba);
                    pixelRow[x] = color;
                    offset += 3;
                }
            }
        }
    }
}
