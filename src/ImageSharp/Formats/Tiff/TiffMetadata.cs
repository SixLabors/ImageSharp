// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
            32 or 64 => new TiffMetadata
            {
                BitsPerPixel = TiffBitsPerPixel.Bit32,
                BitsPerSample = TiffConstants.BitsPerSampleRgb8Bit,
                PhotometricInterpretation = TiffPhotometricInterpretation.Rgb,
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

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = (int)this.BitsPerPixel;

        TiffBitsPerSample samples = this.BitsPerSample;
        PixelComponentInfo info = samples.Channels switch
        {
            1 => PixelComponentInfo.Create(1, bpp, bpp),
            2 => PixelComponentInfo.Create(2, bpp, bpp, samples.Channel0, samples.Channel1),
            3 => PixelComponentInfo.Create(3, bpp, samples.Channel0, samples.Channel1, samples.Channel2),
            _ => PixelComponentInfo.Create(4, bpp, samples.Channel0, samples.Channel1, samples.Channel2, samples.Channel3)
        };

        PixelColorType colorType;
        PixelAlphaRepresentation alpha = PixelAlphaRepresentation.None;
        switch (this.BitsPerPixel)
        {
            case TiffBitsPerPixel.Bit1:
                colorType = PixelColorType.Binary;
                break;
            case TiffBitsPerPixel.Bit4:
            case TiffBitsPerPixel.Bit6:
            case TiffBitsPerPixel.Bit8:
                colorType = PixelColorType.Indexed;
                break;
            case TiffBitsPerPixel.Bit16:
                colorType = PixelColorType.Luminance;
                break;
            case TiffBitsPerPixel.Bit32:
            case TiffBitsPerPixel.Bit64:
                colorType = PixelColorType.RGB | PixelColorType.Alpha;
                alpha = PixelAlphaRepresentation.Unassociated;
                break;
            default:
                colorType = PixelColorType.RGB;
                break;
        }

        return new PixelTypeInfo(bpp)
        {
            ColorType = colorType,
            ComponentInfo = info,
            AlphaRepresentation = alpha
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public TiffMetadata DeepClone() => new(this);
}
