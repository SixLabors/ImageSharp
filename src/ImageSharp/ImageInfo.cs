// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Metadata;

namespace SixLabors.ImageSharp;

/// <summary>
/// Contains information about the image including dimensions, pixel type information and additional metadata
/// </summary>
public class ImageInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInfo"/> class.
    /// </summary>
    /// <param name="pixelType">The pixel type information.</param>
    /// <param name="width">The width of the image in px units.</param>
    /// <param name="height">The height of the image in px units.</param>
    /// <param name="metadata">The image metadata.</param>
    public ImageInfo(PixelTypeInfo pixelType, int width, int height, ImageMetadata? metadata)
        : this(pixelType, new(width, height), metadata)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageInfo"/> class.
    /// </summary>
    /// <param name="pixelType">The pixel type information.</param>
    /// <param name="size">The size of the image in px units.</param>
    /// <param name="metadata">The image metadata.</param>
    public ImageInfo(PixelTypeInfo pixelType, Size size, ImageMetadata? metadata)
    {
        this.PixelType = pixelType;
        this.Metadata = metadata ?? new ImageMetadata();
        this.Size = size;
    }

    /// <summary>
    /// Gets information about the image pixels.
    /// </summary>
    public PixelTypeInfo PixelType { get; }

    /// <summary>
    /// Gets the image width in px units.
    /// </summary>
    public int Width => this.Size.Width;

    /// <summary>
    /// Gets the image height in px units.
    /// </summary>
    public int Height => this.Size.Height;

    /// <summary>
    /// Gets any metadata associated wit The image.
    /// </summary>
    public ImageMetadata Metadata { get; }

    /// <summary>
    /// Gets the size of the image in px units.
    /// </summary>
    public Size Size { get; internal set; }

    /// <summary>
    /// Gets the bounds of the image.
    /// </summary>
    public Rectangle Bounds => new(0, 0, this.Width, this.Height);
}
