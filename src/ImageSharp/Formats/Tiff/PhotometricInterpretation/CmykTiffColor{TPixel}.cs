// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Formats.Tiff.Compression;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

internal class CmykTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private static readonly ColorProfileConverter ColorProfileConverter = new();
    private const float Inv255 = 1f / 255f;

    private readonly TiffDecoderCompressionType compression;

    public CmykTiffColor(TiffDecoderCompressionType compression) => this.compression = compression;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        int offset = 0;

        if (this.compression == TiffDecoderCompressionType.Jpeg)
        {
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    pixelRow[x] = TPixel.FromVector4(new(data[offset] * Inv255, data[offset + 1] * Inv255, data[offset + 2] * Inv255, 1.0f));

                    offset += 3;
                }
            }

            return;
        }

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                Cmyk cmyk = new(data[offset] * Inv255, data[offset + 1] * Inv255, data[offset + 2] * Inv255, data[offset + 3] * Inv255);
                Rgb rgb = ColorProfileConverter.Convert<Cmyk, Rgb>(in cmyk);
                pixelRow[x] = TPixel.FromScaledVector4(new(rgb.R, rgb.G, rgb.B, 1.0f));

                offset += 4;
            }
        }
    }
}
