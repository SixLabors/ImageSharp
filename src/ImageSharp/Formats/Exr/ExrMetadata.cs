// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Provides OpenExr specific metadata information for the image.
/// </summary>
public class ExrMetadata : IFormatMetadata<ExrMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrMetadata"/> class.
    /// </summary>
    public ExrMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private ExrMetadata(ExrMetadata other) => this.PixelType = other.PixelType;

    /// <summary>
    /// Gets or sets the pixel format.
    /// </summary>
    public ExrPixelType PixelType { get; set; } = ExrPixelType.Half;

    /// <summary>
    /// Gets or sets the image data type, either RGB, RGBA or gray.
    /// </summary>
    public ExrImageDataType ImageDataType { get; set; } = ExrImageDataType.Unknown;

    /// <summary>
    /// Gets or sets the compression method.
    /// </summary>
    public ExrCompression Compression { get; set; } = ExrCompression.None;

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        bool hasAlpha = this.ImageDataType is ExrImageDataType.Rgba;

        int bitsPerComponent = 32;
        int bitsPerPixel = hasAlpha ? bitsPerComponent * 4 : bitsPerComponent * 3;
        if (this.PixelType == ExrPixelType.Half)
        {
            bitsPerComponent = 16;
            bitsPerPixel = hasAlpha ? bitsPerComponent * 4 : bitsPerComponent * 3;
        }

        PixelAlphaRepresentation alpha = hasAlpha ? PixelAlphaRepresentation.Unassociated : PixelAlphaRepresentation.None;
        PixelColorType color = PixelColorType.RGB;

        int componentsCount = 0;
        int[] precision = [];
        switch (this.ImageDataType)
        {
            case ExrImageDataType.Rgb:
                color = PixelColorType.RGB;
                componentsCount = 3;
                precision = new int[componentsCount];
                precision[0] = bitsPerComponent;
                precision[1] = bitsPerComponent;
                precision[2] = bitsPerComponent;
                break;
            case ExrImageDataType.Rgba:
                color = PixelColorType.RGB | PixelColorType.Alpha;
                componentsCount = 4;
                precision = new int[componentsCount];
                precision[0] = bitsPerComponent;
                precision[1] = bitsPerComponent;
                precision[2] = bitsPerComponent;
                precision[3] = bitsPerComponent;
                break;
            case ExrImageDataType.Gray:
                color = PixelColorType.Luminance;
                componentsCount = 1;
                precision = new int[componentsCount];
                precision[0] = bitsPerComponent;
                break;
        }

        PixelComponentInfo info = PixelComponentInfo.Create(componentsCount, bitsPerPixel, precision);
        return new PixelTypeInfo(bitsPerPixel)
        {
            AlphaRepresentation = alpha,
            ComponentInfo = info,
            ColorType = color
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
    {
        EncodingType type = this.Compression is ExrCompression.B44 or ExrCompression.B44A or ExrCompression.Pxr24
            ? EncodingType.Lossy
            : EncodingType.Lossless;

        return new()
        {
            EncodingType = type,
            PixelTypeInfo = this.GetPixelTypeInfo()
        };
    }

    /// <inheritdoc/>
    public static ExrMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
    {
        PixelTypeInfo pixelTypeInfo = metadata.PixelTypeInfo;
        PixelComponentInfo? info = pixelTypeInfo.ComponentInfo;
        PixelColorType colorType = pixelTypeInfo.ColorType;

        int bitsPerComponent = info?.GetMaximumComponentPrecision()
            ?? (pixelTypeInfo.BitsPerPixel <= 16 ? 16 : 32);

        int componentCount = info?.ComponentCount ?? 0;
        ExrImageDataType imageDataType = colorType switch
        {
            PixelColorType.Luminance => ExrImageDataType.Gray,
            PixelColorType.RGB or PixelColorType.BGR => ExrImageDataType.Rgb,
            PixelColorType.RGB | PixelColorType.Alpha
                or PixelColorType.BGR | PixelColorType.Alpha
                or PixelColorType.Luminance | PixelColorType.Alpha => ExrImageDataType.Rgba,
            _ => componentCount switch
            {
                >= 4 => ExrImageDataType.Rgba,
                >= 3 => ExrImageDataType.Rgb,
                1 => ExrImageDataType.Gray,
                _ => ExrImageDataType.Unknown,
            }
        };

        return new()
        {
            PixelType = bitsPerComponent <= 16 ? ExrPixelType.Half : ExrPixelType.Float,
            ImageDataType = imageDataType,
        };
    }

    /// <inheritdoc/>
    ExrMetadata IDeepCloneable<ExrMetadata>.DeepClone() => new(this);

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new ExrMetadata(this);

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }
}
