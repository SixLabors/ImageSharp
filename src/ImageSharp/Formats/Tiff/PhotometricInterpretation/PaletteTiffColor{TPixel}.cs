// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff.PhotometricInterpretation;

/// <summary>
/// Implements the 'PaletteTiffColor' photometric interpretation (for all bit depths).
/// </summary>
/// <typeparam name="TPixel">The type of pixel format.</typeparam>
internal class PaletteTiffColor<TPixel> : TiffBaseColorDecoder<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    private readonly ushort bitsPerSample0;

    private readonly TPixel[] palette;

    private const float InvMax = 1f / 65535f;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
    /// <param name="colorMap">The RGB color lookup table to use for decoding the image.</param>
    public PaletteTiffColor(TiffBitsPerSample bitsPerSample, ushort[] colorMap)
    {
        this.bitsPerSample0 = bitsPerSample.Channel0;
        int colorCount = 1 << this.bitsPerSample0;
        this.palette = GeneratePalette(colorMap, colorCount);
    }

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                int index = bitReader.ReadBits(this.bitsPerSample0);
                pixelRow[x] = this.palette[index];
            }

            bitReader.NextRow();
        }
    }

    private static TPixel[] GeneratePalette(ushort[] colorMap, int colorCount)
    {
        TPixel[] palette = new TPixel[colorCount];

        const int rOffset = 0;
        int gOffset = colorCount;
        int bOffset = colorCount * 2;

        for (int i = 0; i < palette.Length; i++)
        {
            float r = colorMap[rOffset + i] * InvMax;
            float g = colorMap[gOffset + i] * InvMax;
            float b = colorMap[bOffset + i] * InvMax;
            palette[i] = TPixel.FromScaledVector4(new Vector4(r, g, b, 1f));
        }

        return palette;
    }
}
