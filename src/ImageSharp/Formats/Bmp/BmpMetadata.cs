// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Provides Bmp specific metadata information for the image.
/// </summary>
public class BmpMetadata : IFormatMetadata<BmpMetadata>, IFormatFrameMetadata<BmpMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
    /// </summary>
    public BmpMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BmpMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private BmpMetadata(BmpMetadata other)
    {
        this.BitsPerPixel = other.BitsPerPixel;
        this.InfoHeaderType = other.InfoHeaderType;
    }

    /// <summary>
    /// Gets or sets the bitmap info header type.
    /// </summary>
    public BmpInfoHeaderType InfoHeaderType { get; set; }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public BmpBitsPerPixel BitsPerPixel { get; set; } = BmpBitsPerPixel.Pixel24;

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingMetadata(FormatConnectingMetadata metadata)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public static BmpMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingMetadata ToFormatConnectingMetadata()
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => ((IDeepCloneable<BmpMetadata>)this).DeepClone();

    /// <inheritdoc/>
    BmpMetadata IDeepCloneable<BmpMetadata>.DeepClone() => new(this);

    // TODO: Colors used once we support encoding palette bmps.
}
