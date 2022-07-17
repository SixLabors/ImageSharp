// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Numerics;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation
{
    /// <summary>
    /// Implements decoding pixel data with photometric interpretation of type 'CieLab'.
    /// </summary>
    internal class CieLabTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private static readonly ColorSpaceConverter ColorSpaceConverter = new();

        private const float Inv255 = 1.0f / 255.0f;

        /// <inheritdoc/>
        public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
        {
            var color = default(TPixel);
            int offset = 0;
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);

                for (int x = 0; x < pixelRow.Length; x++)
                {
                    float l = (data[offset] & 0xFF) * 100f * Inv255;
                    var lab = new CieLab(l, (sbyte)data[offset + 1], (sbyte)data[offset + 2]);
                    var rgb = ColorSpaceConverter.ToRgb(lab);

                    color.FromVector4(new Vector4(rgb.R, rgb.G, rgb.B, 1.0f));
                    pixelRow[x] = color;

                    offset += 3;
                }
            }
        }
    }
}
