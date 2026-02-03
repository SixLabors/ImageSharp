// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements decoding pixel data with photometric interpretation of type 'CieLab'.
/// Each channel is represented with 8 bits.
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class CieLab8TiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private static readonly ColorProfileConverter ColorProfileConverter = new();
    private const float Inv255 = 1f / 255f;

    /// <summary>
    /// Initializes a new instance of the <see cref="CieLab8TiffColor{TPixel}" /> class.
    /// </summary>
    public CieLab8TiffColor()
    {
    }

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
                Rgb rgb = ColorProfileConverter.Convert<CieLab, Rgb>(in lab);
                pixelRow[x] = TPixel.FromScaledVector4(new Vector4(rgb.R, rgb.G, rgb.B, 1f));

                offset += 3;
            }
        }
    }
}
