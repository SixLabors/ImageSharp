// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.ColorProfiles;
using SixLabors.ImageSharp.ColorProfiles.Icc;
using SixLabors.ImageSharp.Formats.Tiff.Utils;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;
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

    private readonly Color[] paletteColors;
    private readonly TPixel[] pixelPalette;

    private readonly float alphaScale;
    private readonly bool hasAlpha;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaletteTiffColor{TPixel}"/> class.
    /// </summary>
    /// <param name="bitsPerSample">The number of bits per sample for each pixel.</param>
    /// <param name="colorMap">The RGB color lookup table to use for decoding the image.</param>
    /// <param name="extraSamplesType">The type of extra samples.</param>
    /// <param name="metadata">The metadata for the current IFD.</param>
    /// <param name="configuration">The decoder configuration.</param>
    public PaletteTiffColor(TiffBitsPerSample bitsPerSample, ushort[] colorMap, TiffExtraSampleType? extraSamplesType, ImageFrameMetadata metadata, Configuration configuration)
    {
        this.bitsPerSample0 = bitsPerSample.Channel0;
        this.bitsPerSample1 = bitsPerSample.Channel1;
        this.extraSamplesType = extraSamplesType;

        MemoryAllocator allocator = configuration.MemoryAllocator;
        int colorCount = 1 << this.bitsPerSample0;

        // Match libtiff's legacy palette detection. Values in 0-255 represent 8-bit samples;
        // multiplying by 257 maps them to Rgb48's 16-bit range without quantizing other ColorMaps.
        bool legacy8Bit = !colorMap.AsSpan().ContainsAnyExceptInRange((ushort)0, (ushort)255);
        this.paletteColors = new Color[colorCount];

        if (IccProfile.TryGetIccProfileForColorConversion(metadata.IccProfile, out IccProfile? profile))
        {
            ColorConversionOptions options = new()
            {
                SourceIccProfile = profile,
                TargetIccProfile = CompactSrgbV4Profile.Profile,
                MemoryAllocator = allocator
            };

            ColorProfileConverter converter = new(options);

            // A temporary typed source lets the ICC span transform initialize once for the whole ColorMap.
            // It is released after conversion; metadata uses the resulting Color table.
            using IMemoryOwner<Rgb48> sourceBuffer = allocator.Allocate<Rgb48>(colorCount);
            Span<Rgb48> source = sourceBuffer.GetSpan();

            for (int i = 0; i < colorCount; i++)
            {
                ushort r = colorMap[i];
                ushort g = colorMap[colorCount + i];
                ushort b = colorMap[(colorCount * 2) + i];

                source[i] = legacy8Bit
                    ? new Rgb48((ushort)(r * 257), (ushort)(g * 257), (ushort)(b * 257))
                    : new Rgb48(r, g, b);
            }

            converter.Convert(configuration, source, this.paletteColors);
        }
        else
        {
            // ColorMap (tag 320) stores planar RGB without palette alpha. Unprofiled entries
            // go straight to the retained table without staging a second full palette.
            for (int i = 0; i < colorCount; i++)
            {
                ushort r = colorMap[i];
                ushort g = colorMap[colorCount + i];
                ushort b = colorMap[(colorCount * 2) + i];

                Rgb48 source = legacy8Bit
                    ? new Rgb48((ushort)(r * 257), (ushort)(g * 257), (ushort)(b * 257))
                    : new Rgb48(r, g, b);

                this.paletteColors[i] = Color.FromPixel(source);
            }
        }

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
            this.pixelPalette = [];
        }
        else
        {
            // Pre-generate pixel palette for non-alpha case for performance.
            this.pixelPalette = GeneratePixelPalette(this.paletteColors);
        }
    }

    /// <summary>
    /// Gets the decoded color table, including any per-pixel alpha updates from decoded extra samples.
    /// </summary>
    public Color[] PaletteColors => this.paletteColors;

    /// <inheritdoc/>
    public override void Decode(ReadOnlySpan<byte> data, Buffer2D<TPixel> pixels, int left, int top, int width, int height)
    {
        BitReader bitReader = new(data);

        if (this.hasAlpha)
        {
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
                    if ((uint)index >= (uint)this.paletteColors.Length)
                    {
                        index = 0;
                    }

                    Vector4 color = this.paletteColors[index].ToScaledVector4(PixelAlphaRepresentation.Unassociated);
                    color.W = alpha;

                    pixelRow[x] = TPixel.FromUnassociatedScaledVector4(color);

                    // Best-effort palette update for downstream conversions.
                    // This is intentionally "last writer wins" with no per-pixel branch.
                    this.paletteColors[index] = Color.FromScaledVector(color);
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
                if ((uint)index >= (uint)this.pixelPalette.Length)
                {
                    index = 0;
                }

                pixelRow[x] = this.pixelPalette[index];
            }

            bitReader.NextRow();
        }
    }

    /// <summary>
    /// Converts the normalized color palette to pixels with opaque alpha.
    /// </summary>
    /// <param name="colorPalette">The normalized palette, already converted to sRGB when profiled.</param>
    /// <returns>The pixel palette.</returns>
    private static TPixel[] GeneratePixelPalette(Color[] colorPalette)
    {
        TPixel[] palette = new TPixel[colorPalette.Length];
        Color.ToPixel(colorPalette, palette);

        return palette;
    }
}
