// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides Ani specific metadata information for the image.
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
    /// Gets or sets the width of frames in the animation.
    /// </summary>
    /// <remarks>
    /// Remains zero when <see cref="Flags"/> has flag <see cref="AniHeaderFlags.IsIcon"/>
    /// </remarks>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the height of frames in the animation.
    /// </summary>
    /// <remarks>
    /// Remains zero when <see cref="Flags"/> has flag <see cref="AniHeaderFlags.IsIcon"/>
    /// </remarks>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    /// <remarks>
    /// Remains zero when <see cref="Flags"/> has flag <see cref="AniHeaderFlags.IsIcon"/>
    /// </remarks>
    public uint BitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of frames in the animation.
    /// </summary>
    /// <remarks>
    /// Remains zero when <see cref="Flags"/> has flag <see cref="AniHeaderFlags.IsIcon"/>
    /// </remarks>
    public uint Planes { get; set; }

    /// <summary>
    /// Gets or sets the default display rate of frames in the animation.
    /// </summary>
    public uint DisplayRate { get; set; }

    /// <summary>
    /// Gets or sets the flags for the ANI header.
    /// </summary>
    public AniHeaderFlags Flags { get; set; }

    /// <summary>
    /// Gets or sets the name of the ANI file.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the artist of the ANI file.
    /// </summary>
    public string? Artist { get; set; }

    /// <inheritdoc/>
    public static AniMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
            where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo()
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public AniMetadata DeepClone() => new()
    {
        Width = this.Width,
        Height = this.Height,
        BitCount = this.BitCount,
        Planes = this.Planes,
        DisplayRate = this.DisplayRate,
        Flags = this.Flags,
        Name = this.Name,
        Artist = this.Artist

        // TODO IconFrames
    };
}
