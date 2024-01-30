// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'CieLab'.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class CieLabTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private static readonly ColorSpaceConverter ColorSpaceConverter = new();
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
                float l = (data[offset] & 0xFF) * 100f * Inv255;
                CieLab lab = new(l, (sbyte)data[offset + 1], (sbyte)data[offset + 2]);
                Rgb rgb = ColorSpaceConverter.ToRgb(lab);
                pixelRow[x] = TPixel.FromScaledVector4(new(rgb.R, rgb.G, rgb.B, 1f));

                offset += 3;
            }
        }
    }
}
