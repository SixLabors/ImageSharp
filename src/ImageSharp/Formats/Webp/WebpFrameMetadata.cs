// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Provides webp specific metadata information for the image frame.
/// </summary>
public class WebpFrameMetadata : IDeepCloneable
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
        this.DisposalMethod = other.DisposalMethod;
        this.BlendMethod = other.BlendMethod;
    }

    /// <summary>
    /// Gets or sets how transparent pixels of the current frame are to be blended with corresponding pixels of the previous canvas.
    /// </summary>
    public WebpBlendingMethod BlendMethod { get; set; }

    /// <summary>
    /// Gets or sets how the current frame is to be treated after it has been displayed (before rendering the next frame) on the canvas.
    /// </summary>
    public WebpDisposalMethod DisposalMethod { get; set; }

    /// <summary>
    /// Gets or sets the frame duration. The time to wait before displaying the next frame,
    /// in 1 millisecond units. Note the interpretation of frame duration of 0 (and often smaller and equal to  10) is implementation defined.
    /// </summary>
    public uint FrameDelay { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new WebpFrameMetadata(this);

    internal static WebpFrameMetadata FromAnimatedMetadata(AnimatedImageFrameMetadata metadata)
        => new()
        {
            FrameDelay = (uint)metadata.Duration.Milliseconds,
            BlendMethod = metadata.BlendMode == FrameBlendMode.Source ? WebpBlendingMethod.Source : WebpBlendingMethod.Over,
            DisposalMethod = metadata.DisposalMode == FrameDisposalMode.RestoreToBackground ? WebpDisposalMethod.RestoreToBackground : WebpDisposalMethod.DoNotDispose
        };
}
