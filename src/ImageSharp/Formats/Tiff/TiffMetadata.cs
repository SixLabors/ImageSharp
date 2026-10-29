// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Tiff.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Provides Tiff specific metadata information for the image.
/// </summary>
public class TiffMetadata : IFormatMetadata<TiffMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
    /// </summary>
    public TiffMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TiffMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private TiffMetadata(TiffMetadata other)
    {
        this.ByteOrder = other.ByteOrder;
        this.FormatType = other.FormatType;
        this.BitsPerPixel = other.BitsPerPixel;
        this.BitsPerSample = other.BitsPerSample;
        this.SampleFormat = other.SampleFormat;
        this.ExtraSampleType = other.ExtraSampleType;
        this.Compression = other.Compression;
        this.PhotometricInterpretation = other.PhotometricInterpretation;
        this.Predictor = other.Predictor;
    }

    /// <summary>
    /// Gets or sets the byte order.
    /// </summary>
    public ByteOrder ByteOrder { get; set; }

    /// <summary>
    /// Gets or sets the format type.
    /// </summary>
    public TiffFormatType FormatType { get; set; }

    /// <summary>
    /// Gets or sets the bits per pixel. Derived from the root frame.
    /// </summary>
    public TiffBitsPerPixel BitsPerPixel { get; set; } = TiffConstants.DefaultBitsPerPixel;

    /// <summary>
    /// Gets or sets number of bits per component. Derived from the root frame.
    /// </summary>
    public TiffBitsPerSample BitsPerSample { get; set; } = TiffConstants.DefaultBitsPerSample;

    /// <summary>
    /// Gets or sets the root frame's TIFF sample format.
    /// </summary>
    public TiffSampleFormat SampleFormat { get; set; } = TiffSampleFormat.UnsignedInteger;

    /// <summary>
    /// Gets or sets the root frame's interpretation of an extra sample.
    /// </summary>
    internal TiffExtraSampleType? ExtraSampleType { get; set; }

    /// <summary>
    /// Gets or sets the compression scheme used on the image data. Derived from the root frame.
    /// </summary>
    public TiffCompression Compression { get; set; } = TiffConstants.DefaultCompression;

    /// <summary>
    /// Gets or sets the color space of the image data. Derived from the root frame.
    /// </summary>
    public TiffPhotometricInterpretation PhotometricInterpretation { get; set; } = TiffConstants.DefaultPhotometricInterpretation;

    /// <summary>
    /// Gets or sets a mathematical operator that is applied to the image data before an encoding scheme is applied.
    /// Derived from the root frame.
    /// </summary>
    public TiffPredictor Predictor { get; set; } = TiffConstants.DefaultPredictor;

    /// <inheritdoc/>
    public static TiffMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        if (TryGetLayout(
            metadata.PixelTypeInfo,
            out TiffPhotometricInterpretation photometric,
            out TiffBitsPerSample samples,
            out TiffExtraSampleType? extraSample))
        {
            TiffBitsPerPixel bitsPerPixel = samples.BitsPerPixel();
            bool isBilevel = photometric == TiffPhotometricInterpretation.WhiteIsZero && bitsPerPixel == TiffBitsPerPixel.Bit1;

            // CCITT fax compression is valid for bilevel grayscale, never indexed samples.
            return new TiffMetadata
            {
                BitsPerPixel = bitsPerPixel,
                BitsPerSample = samples,
                PhotometricInterpretation = photometric,
                ExtraSampleType = extraSample,
                Compression = isBilevel ? TiffCompression.CcittGroup4Fax : TiffCompression.Deflate,
                Predictor = isBilevel || (int)bitsPerPixel <= 4 ? TiffPredictor.None : TiffPredictor.Horizontal
            };
        }

        int bpp = metadata.PixelTypeInfo.BitsPerPixel;
        return bpp switch
        {
            1 => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit1,
                BitsPerSample = TiffConstants.BitsPerSample1Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.WhiteIsZero,
                Compression = TiffCompression.CcittGroup4Fax,
                Predictor = TiffPredictor.None
            },
            <= 4 => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit4,
                BitsPerSample = TiffConstants.BitsPerSample4Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.PaletteColor,
                Compression = TiffCompression.Deflate,
                Predictor = TiffPredictor.None // Best match for low bit depth
            },
            8 => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit8,
                BitsPerSample = TiffConstants.BitsPerSample8Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.PaletteColor,
                Compression = TiffCompression.Deflate,
                Predictor = TiffPredictor.Horizontal
            },
            16 => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit16,
                BitsPerSample = TiffConstants.BitsPerSample16Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.BlackIsZero,
                Compression = TiffCompression.Deflate,
                Predictor = TiffPredictor.Horizontal
            },
            _ => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit24,
                BitsPerSample = TiffConstants.BitsPerSampleRgb8Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
                Compression = TiffCompression.Deflate,
                Predictor = TiffPredictor.Horizontal
            }
        };
    }

    /// <summary>
    /// Gets the TIFF sample layout represented by named format-connecting pixel metadata.
    /// </summary>
    /// <param name="pixelType">The source pixel metadata.</param>
    /// <param name="photometric">The corresponding TIFF photometric interpretation.</param>
    /// <param name="samples">The TIFF component precision.</param>
    /// <param name="extraSample">The TIFF alpha sample interpretation, if present.</param>
    /// <returns>Whether the source color type has a corresponding TIFF layout.</returns>
    internal static bool TryGetLayout(
        PixelTypeInfo pixelType,
        out TiffPhotometricInterpretation photometric,
        out TiffBitsPerSample samples,
        out TiffExtraSampleType? extraSample)
    {
        PixelColorType colorType = pixelType.ColorType & ~PixelColorType.Alpha;
        bool hasAlpha = (pixelType.ColorType & PixelColorType.Alpha) != 0
            && colorType is PixelColorType.Luminance or PixelColorType.RGB or PixelColorType.BGR;

        PixelComponentInfo? components = pixelType.ComponentInfo;
        int channelCount;

        switch (colorType)
        {
            case PixelColorType.Binary:
                photometric = TiffPhotometricInterpretation.WhiteIsZero;
                channelCount = 1;
                break;
            case PixelColorType.Indexed:
                photometric = TiffPhotometricInterpretation.PaletteColor;
                channelCount = 1;
                break;
            case PixelColorType.Luminance:
                photometric = TiffPhotometricInterpretation.BlackIsZero;
                channelCount = hasAlpha ? 2 : 1;
                break;
            case PixelColorType.RGB:
            case PixelColorType.BGR:
                photometric = TiffPhotometricInterpretation.Rgb;
                channelCount = hasAlpha ? 4 : 3;
                break;
            case PixelColorType.CMYK:
                photometric = TiffPhotometricInterpretation.Separated;
                channelCount = 4;
                break;
            case PixelColorType.YCbCr:
                photometric = TiffPhotometricInterpretation.YCbCr;
                channelCount = 3;
                break;
            case PixelColorType.Lab:
                photometric = TiffPhotometricInterpretation.CieLab;
                channelCount = 3;
                break;
            default:
                photometric = default;
                samples = default;
                extraSample = null;
                return false;
        }

        extraSample = hasAlpha
            ? pixelType.AlphaRepresentation == PixelAlphaRepresentation.Associated
                ? TiffExtraSampleType.AssociatedAlphaData
                : TiffExtraSampleType.UnassociatedAlphaData
            : null;

        // Missing component metadata uses the established 8-bit color convention;
        // the binary layout alone uses a one-bit default.
        ushort channel0 = (ushort)(components?.GetComponentPrecision(0) ?? (colorType == PixelColorType.Binary ? 1 : 8));
        ushort channel1 = channelCount > 1 ? (ushort)(components?.GetComponentPrecision(1) ?? 8) : (ushort)0;
        ushort channel2 = channelCount > 2 ? (ushort)(components?.GetComponentPrecision(2) ?? 8) : (ushort)0;
        ushort channel3 = channelCount > 3 ? (ushort)(components?.GetComponentPrecision(3) ?? 8) : (ushort)0;

        if (colorType == PixelColorType.BGR)
        {
            // TIFF RGB sample order reverses the source BGR component order.
            (channel0, channel2) = (channel2, channel0);
        }

        // The fourth CMYK sample is black ink; only grayscale and RGB layouts carry alpha here.
        samples = new TiffBitsPerSample(channel0, channel1, channel2, channel3);
        return true;
    }

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = (int)this.BitsPerPixel;

        TiffBitsPerSample samples = this.BitsPerSample;
        PixelComponentInfo info = samples.Channels switch
        {
            1 => PixelComponentInfo.Create(1, bpp, samples.Channel0),
            2 => PixelComponentInfo.Create(2, bpp, samples.Channel0, samples.Channel1),
            3 => PixelComponentInfo.Create(3, bpp, samples.Channel0, samples.Channel1, samples.Channel2),
            _ => PixelComponentInfo.Create(4, bpp, samples.Channel0, samples.Channel1, samples.Channel2, samples.Channel3)
        };

        // Total bit depth alone cannot distinguish one 32-bit grayscale sample from
        // four 8-bit color samples. The photometric tag and sample count define the layout.
        bool isGrayscale = this.PhotometricInterpretation is TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero;
        PixelColorType colorType = this.PhotometricInterpretation switch
        {
            TiffPhotometricInterpretation.BlackIsZero or TiffPhotometricInterpretation.WhiteIsZero => bpp == 1
                ? PixelColorType.Binary
                : PixelColorType.Luminance,
            TiffPhotometricInterpretation.PaletteColor => PixelColorType.Indexed,
            TiffPhotometricInterpretation.Rgb => PixelColorType.RGB,
            TiffPhotometricInterpretation.Separated => PixelColorType.CMYK,
            TiffPhotometricInterpretation.YCbCr => PixelColorType.YCbCr,
            TiffPhotometricInterpretation.CieLab => PixelColorType.Lab,
            _ => PixelColorType.Other
        };

        bool hasAlpha = (isGrayscale && samples.Channels == 2)
            || (this.PhotometricInterpretation == TiffPhotometricInterpretation.Rgb && samples.Channels == 4);

        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        if (hasAlpha)
        {
            colorType |= PixelColorType.Alpha;
            alpha = this.ExtraSampleType == TiffExtraSampleType.AssociatedAlphaData
                ? PixelAlphaRepresentation.Associated
                : PixelAlphaRepresentation.Unassociated;
        }

        return new PixelTypeInfo(bpp)
        {
            ColorType = colorType,
            ComponentInfo = info,
            AlphaRepresentation = alpha,

            // Floating-point samples retain values outside [0,1]. Integer samples use normalized component values.
            ColorRange = this.SampleFormat == TiffSampleFormat.Float ? PixelColorRange.Extended : PixelColorRange.Sdr
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public TiffMetadata DeepClone() => new(this);
}
