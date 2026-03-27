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

    /// <inheritdoc/>
    public void AfterImageApply<TPixel>(Image<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel> => throw new NotImplementedException();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo() => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata() => throw new NotImplementedException();

    /// <inheritdoc/>
    public static ExrMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => throw new NotImplementedException();

    /// <inheritdoc/>
    ExrMetadata IDeepCloneable<ExrMetadata>.DeepClone() => throw new NotImplementedException();

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new ExrMetadata(this);
}
