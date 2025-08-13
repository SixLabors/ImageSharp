// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Png.Chunks;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Provides Png specific metadata information for the image.
/// </summary>
public class PngMetadata : IFormatMetadata<PngMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PngMetadata"/> class.
    /// </summary>
    public PngMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PngMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private PngMetadata(PngMetadata other)
    {
        this.BitDepth = other.BitDepth;
        this.ColorType = other.ColorType;
        this.Gamma = other.Gamma;
        this.InterlaceMethod = other.InterlaceMethod;
        this.TransparentColor = other.TransparentColor;
        this.RepeatCount = other.RepeatCount;
        this.AnimateRootFrame = other.AnimateRootFrame;

        if (other.ColorTable?.Length > 0)
        {
            this.ColorTable = other.ColorTable.Value.ToArray();
        }

        for (int i = 0; i < other.TextData.Count; i++)
        {
            this.TextData.Add(other.TextData[i]);
        }
    }

    /// <summary>
    /// Gets or sets the number of bits per sample or per palette index (not per pixel).
    /// Not all values are allowed for all <see cref="ColorType"/> values.
    /// </summary>
    public PngBitDepth BitDepth { get; set; } = PngBitDepth.Bit8;

    /// <summary>
    /// Gets or sets the color type.
    /// </summary>
    public PngColorType ColorType { get; set; } = PngColorType.RgbWithAlpha;

    /// <summary>
    /// Gets or sets a value indicating whether this instance should write an Adam7 interlaced image.
    /// </summary>
    public PngInterlaceMode InterlaceMethod { get; set; } = PngInterlaceMode.None;

    /// <summary>
    /// Gets or sets the gamma value for the image.
    /// </summary>
    public float Gamma { get; set; }

    /// <summary>
    /// Gets or sets the color table, if any.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <summary>
    /// Gets or sets the transparent color used with non palette based images, if a transparency chunk and markers were decoded.
    /// </summary>
    public Color? TransparentColor { get; set; }

    /// <summary>
    /// Gets or sets the collection of text data stored within the iTXt, tEXt, and zTXt chunks.
    /// Used for conveying textual information associated with the image.
    /// </summary>
    public IList<PngTextData> TextData { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of times to loop this APNG.  0 indicates infinite looping.
    /// </summary>
    public uint RepeatCount { get; set; } = 1;

    /// <summary>
    ///  Gets or sets a value indicating whether the root frame is shown as part of the animated sequence
    /// </summary>
    public bool AnimateRootFrame { get; set; } = true;

    /// <inheritdoc/>
    public static PngMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        PngColorType color;
        PixelColorType colorType = metadata.PixelTypeInfo.ColorType;

        switch (colorType)
        {
            case PixelColorType.Binary:
            case PixelColorType.Indexed:
                color = PngColorType.Palette;
                break;
            case PixelColorType.Luminance:
                color = PngColorType.Grayscale;
                break;
            case PixelColorType.RGB:
            case PixelColorType.BGR:
                color = PngColorType.Rgb;
                break;
            default:
                if (colorType.HasFlag(PixelColorType.Luminance))
                {
                    color = PngColorType.GrayscaleWithAlpha;
                    break;
                }

                color = PngColorType.RgbWithAlpha;
                break;
        }

        // PNG uses bits per component not per pixel.
        int bpc = metadata.PixelTypeInfo.ComponentInfo?.GetMaximumComponentPrecision() ?? 8;
        PngBitDepth bitDepth = bpc switch
        {
            1 => PngBitDepth.Bit1,
            2 => PngBitDepth.Bit2,
            4 => PngBitDepth.Bit4,
            _ => (bpc <= 8) ? PngBitDepth.Bit8 : PngBitDepth.Bit16,
        };
        return new PngMetadata
        {
            ColorType = color,
            BitDepth = bitDepth,
            RepeatCount = metadata.RepeatCount,
        };
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp;
        PixelColorType colorType;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        PixelComponentInfo info;
        switch (this.ColorType)
        {
            case PngColorType.Palette:
                bpp = this.ColorTable.HasValue
                    ? Numerics.Clamp(ColorNumerics.GetBitsNeededForColorDepth(this.ColorTable.Value.Length), 1, 8)
                    : 8;

                colorType = PixelColorType.Indexed;
                info = PixelComponentInfo.Create(1, bpp, bpp);
                break;

            case PngColorType.Grayscale:
                bpp = (int)this.BitDepth;
                colorType = PixelColorType.Luminance;
                info = PixelComponentInfo.Create(1, bpp, bpp);
                break;

            case PngColorType.GrayscaleWithAlpha:

                alpha = PixelAlphaRepresentation.Unassociated;
                if (this.BitDepth == PngBitDepth.Bit16)
                {
                    bpp = 32;
                    colorType = PixelColorType.Luminance | PixelColorType.Alpha;
                    info = PixelComponentInfo.Create(2, bpp, 16, 16);
                    break;
                }

                bpp = 16;
                colorType = PixelColorType.Luminance | PixelColorType.Alpha;
                info = PixelComponentInfo.Create(2, bpp, 8, 8);
                break;

            case PngColorType.Rgb:
                if (this.BitDepth == PngBitDepth.Bit16)
                {
                    bpp = 48;
                    colorType = PixelColorType.RGB;
                    info = PixelComponentInfo.Create(3, bpp, 16, 16, 16);
                    break;
                }

                bpp = 24;
                colorType = PixelColorType.RGB;
                info = PixelComponentInfo.Create(3, bpp, 8, 8, 8);
                break;

            case PngColorType.RgbWithAlpha:
            default:

                alpha = PixelAlphaRepresentation.Unassociated;
                if (this.BitDepth == PngBitDepth.Bit16)
                {
                    bpp = 64;
                    colorType = PixelColorType.RGB | PixelColorType.Alpha;
                    info = PixelComponentInfo.Create(4, bpp, 16, 16, 16, 16);
                    break;
                }

                bpp = 32;
                colorType = PixelColorType.RGB | PixelColorType.Alpha;
                info = PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8);
                break;
        }

        return new PixelTypeInfo(bpp)
        {
            AlphaRepresentation = alpha,
            ColorType = colorType,
            ComponentInfo = info,
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            PixelTypeInfo = this.GetPixelTypeInfo(),
            RepeatCount = (ushort)Numerics.Clamp(this.RepeatCount, 0, ushort.MaxValue),
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        this.ColorTable = null;

        // If the color type is RGB and we have a transparent color, we need to switch to RGBA
        // so that we do not incorrectly preserve the obsolete tRNS chunk.
        if (this.ColorType == PngColorType.Rgb && this.TransparentColor.HasValue)
        {
            this.ColorType = PngColorType.RgbWithAlpha;
            this.TransparentColor = null;
        }

        // The same applies for Grayscale.
        if (this.ColorType == PngColorType.Grayscale && this.TransparentColor.HasValue)
        {
            this.ColorType = PngColorType.GrayscaleWithAlpha;
            this.TransparentColor = null;
        }
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public PngMetadata DeepClone() => new(this);
}
