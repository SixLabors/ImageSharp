// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

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
    }

    /// <summary>
    /// Gets or sets the byte order.
    /// </summary>
    public ByteOrder ByteOrder { get; set; }

    /// <summary>
    /// Gets or sets the format type.
    /// </summary>
    public TiffFormatType FormatType { get; set; }

    /// <inheritdoc/>
    public static TiffMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata) => throw new NotImplementedException();

    /// <inheritdoc/>
    public PixelTypeInfo GetPixelTypeInfo() => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata() => throw new NotImplementedException();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public TiffMetadata DeepClone() => new(this);
}
