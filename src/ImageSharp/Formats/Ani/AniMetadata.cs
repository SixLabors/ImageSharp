// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides ANI-specific metadata for an image.
/// </summary>
public class AniMetadata : IFormatMetadata<AniMetadata>
{
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
        this.Width = other.Width;
        this.Height = other.Height;
        this.BitCount = other.BitCount;
        this.Planes = other.Planes;
        this.DisplayRate = other.DisplayRate;
        this.Flags = other.Flags;
        this.Name = other.Name;
        this.Artist = other.Artist;
    }

    /// <summary>
    /// Gets or sets the frame width declared by the ANI header.
    /// </summary>
    /// <remarks>
    /// Icon-based ANI files commonly store zero because each embedded resource declares its own dimensions.
    /// </remarks>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the frame height declared by the ANI header.
    /// </summary>
    /// <remarks>
    /// Icon-based ANI files commonly store zero because each embedded resource declares its own dimensions.
    /// </remarks>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the bits per pixel declared by the ANI header.
    /// </summary>
    /// <remarks>
    /// Bitmap-based ANI files use this value to describe their raw frame data. Icon-based files commonly store zero
    /// because each embedded ICO or CUR entry declares its own pixel layout.
    /// </remarks>
    public uint BitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of independently addressable color planes declared by the ANI header.
    /// </summary>
    /// <remarks>
    /// Bitmap-based ANI files use the Windows DIB plane value, which must be one. Icon-based ANI files use zero because
    /// each embedded ICO or CUR entry describes its own pixel layout. No other values are defined by the format.
    /// </remarks>
    public uint Planes { get; set; }

    /// <summary>
    /// Gets or sets the default frame display rate in sixtieths of a second.
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// Gets or sets the ANI header flags.
    /// </summary>
    public AniHeaderFlags Flags { get; set; } = AniHeaderFlags.IsIcon;

    /// <summary>
    /// Gets or sets the animation name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the animation artist.
    /// </summary>
    public string? Artist { get; set; }

    /// <inheritdoc/>
    public static AniMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => new()
        {
            BitCount = (uint)metadata.PixelTypeInfo.BitsPerPixel,
            Planes = 1,
            Flags = AniHeaderFlags.IsIcon
        };

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
    {
        // Icon-based files are allowed to leave the global bit depth unspecified. Their embedded
        // ICO/CUR metadata carries the exact value, while 32-bit is the least lossy conversion default.
        int bitsPerPixel = this.BitCount is > 0 and <= 32 ? (int)this.BitCount : 32;
        return new PixelTypeInfo(bitsPerPixel);
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
        if (!this.Flags.HasFlag(AniHeaderFlags.IsIcon))
        {
            this.Width = (uint)destination.Width;
            this.Height = (uint)destination.Height;
        }
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public AniMetadata DeepClone() => new(this);
}
