// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Provides Ani specific metadata information for the image.
/// </summary>
public class AniMetadata : IFormatMetadata<AniMetadata>
{
    /// <summary>
    /// Gets or sets the width of frames in the animation.
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the height of frames in the animation.
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public uint BitCount { get; set; }

    /// <summary>
    /// Gets or sets the number of frames in the animation.
    /// </summary>
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

    /// <summary>
    /// Gets or sets the <see cref="ImageFrameMetadata"/> each "icon" chunk in ANI file.
    /// </summary>
    public IList<ImageFrameMetadata> IconFrames { get; set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AniMetadata"/> class.
    /// </summary>
    public AniMetadata()
    {
    }

    /// <inheritdoc/>
    public static AniMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => throw new NotImplementedException();

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination)
        where TPixel : unmanaged, IPixel<TPixel> => throw new NotImplementedException();

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => throw new NotImplementedException();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo() => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata() => throw new NotImplementedException();

    /// <inheritdoc/>
    AniMetadata IDeepCloneable<AniMetadata>.DeepClone() => throw new NotImplementedException();
}
