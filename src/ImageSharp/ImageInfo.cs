// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <summary>
/// Contains information about the image including dimensions, pixel type information and additional metadata
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInfo"/> class.
    /// </summary>
    /// <param name="size">The size of the image in px units.</param>
    /// <param name="metadata">The image metadata.</param>
    public ImageInfo(
        Size size,
        ImageMetadata metadata)
        : this(size, metadata, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInfo"/> class.
    /// </summary>
    /// <param name="size">The size of the image in px units.</param>
    /// <param name="metadata">The image metadata.</param>
    /// <param name="frameMetadataCollection">The collection of image frame metadata.</param>
    public ImageInfo(
        Size size,
        ImageMetadata metadata,
        IReadOnlyList<ImageFrameMetadata>? frameMetadataCollection)
    {
        this.Size = size;
        this.Metadata = metadata;

        // PixelTpe is normally set following decoding
        // See ImageDecoder.SetDecoderFormat(Configuration configuration, ImageInfo info).
        if (metadata.DecodedImageFormat is not null)
        {
            this.PixelType = metadata.GetDecodedPixelTypeInfo();
        }

        this.FrameMetadataCollection = frameMetadataCollection ?? [];
    }

    /// <summary>
    /// Gets information about the image pixels.
    /// </summary>
    public PixelTypeInfo PixelType { get; internal set; }

    /// <summary>
    /// Gets the image width in px units.
    /// </summary>
    public int Width => this.Size.Width;

    /// <summary>
    /// Gets the image height in px units.
    /// </summary>
    public int Height => this.Size.Height;

    /// <summary>
    /// Gets the number of frames in the image.
    /// </summary>
    public int FrameCount => this.FrameMetadataCollection.Count;

    /// <summary>
    /// Gets any metadata associated with the image.
    /// </summary>
    public ImageMetadata Metadata { get; }

    /// <summary>
    /// Gets the collection of metadata associated with individual image frames.
    /// </summary>
    public IReadOnlyList<ImageFrameMetadata> FrameMetadataCollection { get; }

    /// <summary>
    /// Gets the size of the image in px units.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the bounds of the image.
    /// </summary>
    public Rectangle Bounds => new(Point.Empty, this.Size);
}
