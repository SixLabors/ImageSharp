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
    /// Gets the number of frame metadata entries available for the image.
    /// </summary>
    /// <remarks>
    /// This value is the same as <see cref="FrameMetadataCollection"/> count and may be <c>0</c> when frame
    /// metadata was not populated by the decoder.
    /// </remarks>
    public int FrameCount => this.FrameMetadataCollection.Count;

    /// <summary>
    /// Gets any metadata associated with the image.
    /// </summary>
    public ImageMetadata Metadata { get; }

    /// <summary>
    /// Gets the metadata associated with the decoded image frames, if available.
    /// </summary>
    /// <remarks>
    /// For multi-frame formats, decoders populate one entry per decoded frame. For single-frame formats, this
    /// collection is typically empty.
    /// </remarks>
    public IReadOnlyList<ImageFrameMetadata> FrameMetadataCollection { get; }

    /// <summary>
    /// Gets the size of the image in px units.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the bounds of the image.
    /// </summary>
    public Rectangle Bounds => new(Point.Empty, this.Size);

    /// <summary>
    /// Gets the total number of bytes required to store the image pixels in memory.
    /// </summary>
    /// <remarks>
    /// This reports the in-memory size of the pixel data represented by this <see cref="ImageInfo"/>, not the
    /// encoded size of the image file. The value is computed from the image dimensions and
    /// <see cref="PixelType"/>. When <see cref="FrameMetadataCollection"/> contains decoded frame metadata, the
    /// per-frame size is multiplied by that count. Otherwise, the value is the in-memory size of the single
    /// image frame represented by this <see cref="ImageInfo"/>.
    /// </remarks>
    /// <returns>The total number of bytes required to store the image pixels in memory.</returns>
    public long GetPixelMemorySize()
    {
        int count = this.FrameMetadataCollection.Count > 0
            ? this.FrameMetadataCollection.Count
            : 1;

        return (long)this.Size.Width * this.Size.Height * (this.PixelType.BitsPerPixel / 8) * count;
    }
}
