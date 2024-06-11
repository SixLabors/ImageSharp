// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides Webp specific metadata information for the image.
/// </summary>
public class WebpMetadata : IFormatFrameMetadata<WebpMetadata>
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
        this.BitsPerPixel = other.BitsPerPixel;
        this.ColorType = other.ColorType;
        this.FileFormat = other.FileFormat;
        this.RepeatCount = other.RepeatCount;
        this.BackgroundColor = other.BackgroundColor;
    }

    /// <summary>
    /// Gets or sets the number of bits per pixel.
    /// </summary>
    public WebpBitsPerPixel BitsPerPixel { get; set; } = WebpBitsPerPixel.Bit32;

    /// <summary>
    /// Gets or sets the color type.
    /// </summary>
    public WebpColorType ColorType { get; set; } = WebpColorType.Rgba;

    /// <summary>
    /// Gets or sets the webp file format used. Either lossless or lossy.
    /// </summary>
    public WebpFileFormatType FileFormat { get; set; } = WebpFileFormatType.Lossless;

    /// <summary>
    /// Gets or sets the loop count. The number of times to loop the animation. 0 means infinitely.
    /// </summary>
    public ushort RepeatCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the default background color of the canvas when animating.
    /// This color may be used to fill the unused space on the canvas around the frames,
    /// as well as the transparent pixels of the first frame.
    /// The background color is also used when the Disposal method is <see cref="WebpDisposalMethod.RestoreToBackground"/>.
    /// </summary>
    public Color BackgroundColor { get; set; }

    internal static WebpMetadata FromAnimatedMetadata(AnimatedImageMetadata metadata)
        => new()
        {
            FileFormat = WebpFileFormatType.Lossless,
            BackgroundColor = metadata.BackgroundColor,
            RepeatCount = metadata.RepeatCount
        };

    /// <inheritdoc/>
    public static WebpMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => throw new NotImplementedException();

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => throw new NotImplementedException();

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public WebpMetadata DeepClone() => new(this);
}
