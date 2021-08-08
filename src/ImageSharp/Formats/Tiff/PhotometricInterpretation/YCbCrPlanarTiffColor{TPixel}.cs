// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    internal class YCbCrPlanarTiffColor<TPixel> : TiffBasePlanarColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private readonly YCbCrConverter converter;

        public YCbCrPlanarTiffColor(Rational[] referenceBlackAndWhite, Rational[] coefficients) => this.converter = new YCbCrConverter(referenceBlackAndWhite, coefficients);

        /// <inheritdoc/>
        public override void Decode(IMemoryOwner<byte>[] data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            Span<byte> yData = data[0].GetSpan();
            Span<byte> cbData = data[1].GetSpan();
            Span<byte> crData = data[2].GetSpan();

            var color = default(TPixel);
            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.GetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    Rgba32 rgba = this.converter.ConvertToRgba32(yData[offset], cbData[offset], crData[offset]);
                    color.FromRgba32(rgba);
                    pixelRow[x] = color;
                    offset++;
                }
            }
        }
    }
}
