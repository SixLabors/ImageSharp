// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides Webp specific metadata information for the image.
/// </summary>
public class WebpMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
    /// </summary>
    public WebpMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private WebpMetadata(WebpMetadata other)
    {
        this.FileFormat = other.FileFormat;
        this.AnimationLoopCount = other.AnimationLoopCount;
        this.AnimationBackground = other.AnimationBackground;
    }

    /// <summary>
    /// Gets or sets the webp file format used. Either lossless or lossy.
    /// </summary>
    public WebpFileFormatType? FileFormat { get; set; }

    /// <summary>
    /// Gets or sets the loop count. The number of times to loop the animation. 0 means infinitely.
    /// </summary>
    public ushort AnimationLoopCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the default background color of the canvas in [Blue, Green, Red, Alpha] byte order.
    /// This color MAY be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is 1.
    /// </summary>
    public Color AnimationBackground { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new WebpMetadata(this);
}
