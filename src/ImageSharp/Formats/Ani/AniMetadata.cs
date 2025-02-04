// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Ico;
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
