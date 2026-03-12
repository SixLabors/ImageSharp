// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides webp specific metadata information for the image frame.
/// </summary>
public class WebpFrameMetadata : IFormatFrameMetadata<WebpFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WebpFrameMetadata"/> class.
    /// </summary>
    public WebpFrameMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebpFrameMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private WebpFrameMetadata(WebpFrameMetadata other)
    {
        this.FrameDelay = other.FrameDelay;
        this.DisposalMode = other.DisposalMode;
        this.BlendMode = other.BlendMode;
    }

    /// <summary>
    /// Gets or sets how transparent pixels of the current frame are to be blended with corresponding pixels
    /// of the previous canvas.
    /// </summary>
    public FrameBlendMode BlendMode { get; set; }

    /// <summary>
    /// Gets or sets how the current frame is to be treated after it has been displayed
    /// (before rendering the next frame) on the canvas.
    /// </summary>
    public FrameDisposalMode DisposalMode { get; set; }

    /// <summary>
    /// Gets or sets the frame duration. The time to wait before displaying the next frame,
    /// in 1 millisecond units. Note the interpretation of frame duration of 0 (and often smaller and equal to  10) is implementation defined.
    /// </summary>
    public uint FrameDelay { get; set; }

    /// <inheritdoc/>
    public static WebpFrameMetadata FromFormatConnectingFrameMetadata(FormatConnectingFrameMetadata metadata)
        => new()
        {
            FrameDelay = (uint)metadata.Duration.TotalMilliseconds,
            BlendMode = metadata.BlendMode,
            DisposalMode = GetMode(metadata.DisposalMode)
        };

    /// <inheritdoc/>
    public FormatConnectingFrameMetadata ToFormatConnectingFrameMetadata()
        => new()
        {
            ColorTableMode = FrameColorTableMode.Global,
            Duration = TimeSpan.FromMilliseconds(this.FrameDelay),
            DisposalMode = this.DisposalMode,
            BlendMode = this.BlendMode,
        };

    /// <inheritdoc/>
    public void AfterFrameApply<TPixel>(ImageFrame<TPixel> source, ImageFrame<TPixel> destination, Matrix4x4 matrix)
        where TPixel : unmanaged, IPixel<TPixel>
    {
    }

    /// <inheritdoc/>
    IDeepCloneable IDeepCloneable.DeepClone() => this.DeepClone();

    /// <inheritdoc/>
    public WebpFrameMetadata DeepClone() => new(this);

    private static FrameDisposalMode GetMode(FrameDisposalMode mode) => mode switch
    {
        FrameDisposalMode.RestoreToBackground => FrameDisposalMode.RestoreToBackground,
        FrameDisposalMode.DoNotDispose => FrameDisposalMode.DoNotDispose,
        _ => FrameDisposalMode.DoNotDispose,
    };
}
