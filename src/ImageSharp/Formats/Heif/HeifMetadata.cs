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
        this.BitDepth = other.BitDepth;
        this.IsMonochrome = other.IsMonochrome;
        this.HasAlpha = other.HasAlpha;
        this.ContentLightLevel = other.ContentLightLevel;
        this.MasteringDisplayColorVolume = other.MasteringDisplayColorVolume;
        this.ContentColorVolume = other.ContentColorVolume;
        this.AmbientViewingEnvironment = other.AmbientViewingEnvironment;
        this.ReferenceViewingEnvironment = other.ReferenceViewingEnvironment;
        this.NominalDiffuseWhite = other.NominalDiffuseWhite;
    }

    /// <summary>
    /// Gets or sets the compression method used for the primary frame.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; set; }

    /// <summary>
    /// Gets or sets the encoded precision of each color component in bits.
    /// </summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether the primary image contains a single luminance component.
    /// </summary>
    public bool IsMonochrome { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the primary image has an alpha channel.
    /// </summary>
    public bool HasAlpha { get; set; }

    /// <summary>
    /// Gets or sets the content light-level information for the primary image, or <see langword="null"/> when it is
    /// not available.
    /// </summary>
    public HeifContentLightLevel? ContentLightLevel { get; set; }

    /// <summary>
    /// Gets or sets the mastering-display color volume for the primary image, or <see langword="null"/> when it is
    /// not available.
    /// </summary>
    public HeifMasteringDisplayColorVolume? MasteringDisplayColorVolume { get; set; }

    /// <summary>
    /// Gets or sets the content color volume for the primary image, or <see langword="null"/> when it is not
    /// available.
    /// </summary>
    public HeifContentColorVolume? ContentColorVolume { get; set; }

    /// <summary>
    /// Gets or sets the nominal ambient viewing environment for the primary image, or <see langword="null"/>
    /// when it is not available.
    /// </summary>
    public HeifAmbientViewingEnvironment? AmbientViewingEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the reference mastering environment for the primary image, or <see langword="null"/> when
    /// it is not available.
    /// </summary>
    public HeifReferenceViewingEnvironment? ReferenceViewingEnvironment { get; set; }

    /// <summary>
    /// Gets or sets the nominal diffuse-white description for the primary image, or <see langword="null"/> when
    /// it is not available.
    /// </summary>
    public HeifNominalDiffuseWhite? NominalDiffuseWhite { get; set; }

    /// <inheritdoc/>
    public static HeifMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => new()
    {
        CompressionMethod = HeifCompressionMethod.LegacyJpeg,
        BitDepth = metadata.PixelTypeInfo.ComponentInfo?.GetMaximumComponentPrecision() ?? 8,
        IsMonochrome = metadata.PixelTypeInfo.ColorType.HasFlag(PixelColorType.Luminance)
            && !metadata.PixelTypeInfo.ColorType.HasFlag(PixelColorType.ChrominanceBlue),
        HasAlpha = metadata.PixelTypeInfo.AlphaRepresentation != PixelAlphaRepresentation.None
    };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        int colorComponentCount = this.IsMonochrome ? 1 : 3;
        int componentCount = colorComponentCount + (this.HasAlpha ? 1 : 0);
        int bitsPerPixel = componentCount * this.BitDepth;
        PixelColorType colorType = this.IsMonochrome ? PixelColorType.Luminance : PixelColorType.RGB;
        PixelComponentInfo info;
        if (this.IsMonochrome)
        {
            info = this.HasAlpha
                ? PixelComponentInfo.Create(2, bitsPerPixel, this.BitDepth, this.BitDepth)
                : PixelComponentInfo.Create(1, bitsPerPixel, this.BitDepth);
        }
        else
        {
            info = this.HasAlpha
                ? PixelComponentInfo.Create(4, bitsPerPixel, this.BitDepth, this.BitDepth, this.BitDepth, this.BitDepth)
                : PixelComponentInfo.Create(3, bitsPerPixel, this.BitDepth, this.BitDepth, this.BitDepth);
        }

        if (this.HasAlpha)
        {
            colorType |= PixelColorType.Alpha;
        }

        return new PixelTypeInfo(bitsPerPixel)
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
