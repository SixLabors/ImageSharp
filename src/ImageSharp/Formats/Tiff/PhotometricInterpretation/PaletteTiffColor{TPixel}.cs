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
    private readonly ushort bitsPerSample1;
    private readonly TiffExtraSampleType? extraSamplesType;

    private readonly Vector4[] paletteVectors;
    private readonly float alphaScale;
    private readonly bool hasAlpha;
    private Color[]? paletteColors;

    private const float InvMax = 1f / 65535f;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
    /// <param name="colorMap">The RGB color lookup table to use for decoding the image.</param>
    /// <param name="extraSamplesType">The type of extra samples.</param>
    public PaletteTiffColor(TiffBitsPerSample bitsPerSample, ushort[] colorMap, TiffExtraSampleType? extraSamplesType)
    {
        this.bitsPerSample0 = bitsPerSample.Channel0;
        this.bitsPerSample1 = bitsPerSample.Channel1;
        this.extraSamplesType = extraSamplesType;

        int colorCount = 1 << this.bitsPerSample0;

        // TIFF PaletteColor uses ColorMap (tag 320 / 0x0140) which is RGB-only (no alpha).
        this.paletteVectors = GeneratePaletteVectors(colorMap, colorCount);

        // ExtraSamples (tag 338 / 0x0152) describes extra per-pixel samples stored in the image data stream.
        // For PaletteColor, any alpha is per pixel (stored alongside the index), not per palette entry.
        this.hasAlpha =
            this.bitsPerSample1 > 0
            && this.extraSamplesType.HasValue
            && this.extraSamplesType != TiffExtraSampleType.UnspecifiedData;

        if (this.hasAlpha)
        {
            ulong alphaMax = (1UL << this.bitsPerSample1) - 1;
            this.alphaScale = alphaMax > 0 ? 1f / alphaMax : 1f;
        }
    }

    public Color[] PaletteColors => this.paletteColors ??= GenerateColorPalette(this.paletteVectors);

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);

        if (this.hasAlpha)
        {
            Color[] colors = this.paletteColors ??= GenerateColorPalette(this.paletteVectors);

            // NOTE: ExtraSamples may report "AssociatedAlphaData". For PaletteColor, the stored color sample is the
            // palette index, not per-pixel RGB components, so the premultiplication concept is not representable
            // in the encoded stream. We therefore treat the alpha sample as a per-pixel alpha value applied after
            // palette expansion.
            for (int y = top; y < top + height; y++)
            {
                Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    int index = bitReader.ReadBits(this.bitsPerSample0);
                    float alpha = bitReader.ReadBits(this.bitsPerSample1) * this.alphaScale;

                    // Defensive guard against malformed streams.
                    if ((uint)index >= (uint)this.paletteVectors.Length)
                    {
                        index = 0;
                    }

                    Vector4 color = this.paletteVectors[index];
                    color.W = alpha;

                    pixelRow[x] = TPixel.FromScaledVector4(color);

                    // Best-effort palette update for downstream conversions.
                    // This is intentionally "last writer wins" with no per-pixel branch.
                    colors[index] = Color.FromScaledVector(color);
                }

                bitReader.NextRow();
            }

            return;
        }

        for (int y = top; y < top + height; y++)
        {
            Span<TPixel> pixelRow = pixels.DangerousGetRowSpan(y).Slice(left, width);
            for (int x = 0; x < pixelRow.Length; x++)
            {
                int index = bitReader.ReadBits(this.bitsPerSample0);

                // Defensive guard against malformed streams.
                if ((uint)index >= (uint)this.paletteVectors.Length)
                {
                    index = 0;
                }

                pixelRow[x] = TPixel.FromScaledVector4(this.paletteVectors[index]);
            }

            bitReader.NextRow();
        }
    }

    private static Vector4[] GeneratePaletteVectors(ushort[] colorMap, int colorCount)
    {
        Vector4[] palette = new Vector4[colorCount];

        const int rOffset = 0;
        int gOffset = colorCount;
        int bOffset = colorCount * 2;

        for (int i = 0; i < palette.Length; i++)
        {
            float r = colorMap[rOffset + i] * InvMax;
            float g = colorMap[gOffset + i] * InvMax;
            float b = colorMap[bOffset + i] * InvMax;
            palette[i] = new Vector4(r, g, b, 1f);
        }

        return palette;
    }

    private static Color[] GenerateColorPalette(Vector4[] palette)
    {
        Color[] colors = new Color[palette.Length];
        Color.FromScaledVector(palette, colors);
        return colors;
    }
}
