// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Provides HEIF specific metadata information for the image.
/// </summary>
public class HeifMetadata : IFormatMetadata<HeifMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifMetadata"/> class.
    /// </summary>
    public HeifMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private HeifMetadata(HeifMetadata other)
    {
        this.CompressionMethod = other.CompressionMethod;
        this.HasAlpha = other.HasAlpha;
    }

    /// <summary>
    /// Gets or sets the compression method used for the primary frame.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the primary image has an alpha channel.
    /// </summary>
    public bool HasAlpha { get; set; }

    /// <inheritdoc/>
    public static HeifMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => new()
    {
        CompressionMethod = HeifCompressionMethod.LegacyJpeg,
        HasAlpha = metadata.PixelTypeInfo.AlphaRepresentation != PixelAlphaRepresentation.None
    };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int bpp = this.HasAlpha ? 32 : 24;
        PixelColorType colorType = this.HasAlpha ? PixelColorType.RGB | PixelColorType.Alpha : PixelColorType.RGB;
        PixelComponentInfo info = this.HasAlpha
            ? PixelComponentInfo.Create(4, bpp, 8, 8, 8, 8)
            : PixelComponentInfo.Create(3, bpp, 8, 8, 8);

        return new PixelTypeInfo(bpp)
        {
            AlphaRepresentation = this.HasAlpha ? PixelAlphaRepresentation.Unassociated : PixelAlphaRepresentation.None,
            ColorType = colorType,
            ComponentInfo = info,
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            PixelTypeInfo = this.GetPixelTypeInfo(),
        };

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public HeifMetadata DeepClone() => new(this);

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }
}
