// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Text;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides ANI-specific metadata for an image.
/// </summary>
public class AniMetadata : IFormatMetadata<AniMetadata>
{
    private string? name;
    private string? artist;

    /// <summary>
    /// Initializes a new instance of the <see cref="AniMetadata"/> class.
    /// </summary>
    public AniMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AniMetadata"/> class by copying another instance.
    /// </summary>
    /// <param name="other">The metadata to copy.</param>
    private AniMetadata(AniMetadata other)
    {
        this.DisplayRate = other.DisplayRate;
        this.Name = other.Name;
        this.Artist = other.Artist;
    }

    /// <summary>
    /// Gets or sets the default frame display rate in sixtieths of a second.
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// Gets or sets the animation name.
    /// </summary>
    /// <exception cref="ArgumentException">The value contains a character that is not ASCII.</exception>
    public string? Name
    {
        get => this.name;
        set
        {
            // The encoder writes the INFO text as ASCII.
            Guard.IsTrue(value is null || Ascii.IsValid(value), nameof(value), "The name must contain ASCII characters only.");

            this.name = value;
        }
    }

    /// <summary>
    /// Gets or sets the animation artist.
    /// </summary>
    /// <exception cref="ArgumentException">The value contains a character that is not ASCII.</exception>
    public string? Artist
    {
        get => this.artist;
        set
        {
            // The encoder writes the INFO text as ASCII.
            Guard.IsTrue(value is null || Ascii.IsValid(value), nameof(value), "The artist must contain ASCII characters only.");

            this.artist = value;
        }
    }

    /// <inheritdoc/>
    public static AniMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => new();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        // Each frame holds its own pixel layout in its frame metadata. This value is the layout of a default frame,
        // which the encoder writes as a 32-bit PNG with alpha.
        return new PixelTypeInfo(32)
        {
            AlphaRepresentation = PixelAlphaRepresentation.Unassociated,
            ComponentInfo = PixelComponentInfo.Create(4, 32, 8, 8, 8, 8),
            ColorType = PixelColorType.RGB | PixelColorType.Alpha
        };
    }

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => new()
        {
            AnimateRootFrame = true,
            EncodingType = EncodingType.Lossless,
            PixelTypeInfo = this.GetPixelTypeInfo()
        };

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // The image-level metadata holds no geometry. Each frame holds its own encoding size.
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public AniMetadata DeepClone() => new(this);
}
