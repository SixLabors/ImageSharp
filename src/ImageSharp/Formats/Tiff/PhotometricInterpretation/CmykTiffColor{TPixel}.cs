// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

internal class CmykTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private const float Inv255 = 1f / 255f;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;
        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                Cmyk cmyk = new(data[offset] * Inv255, data[offset + 1] * Inv255, data[offset + 2] * Inv255, data[offset + 3] * Inv255);
                Rgb rgb = ColorSpaceConverter.ToRgb(in cmyk);
                pixelRow[x] = TPixel.FromScaledVector4(new(rgb.R, rgb.G, rgb.B, 1.0f));

                offset += 4;
            }
        }
    }
}
