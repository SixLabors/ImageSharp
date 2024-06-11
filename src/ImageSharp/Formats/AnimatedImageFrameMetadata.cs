// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

internal class AnimatedImageFrameMetadata
{
    /// <summary>
    /// Gets or sets the frame color table.
    /// </summary>
    public ReadOnlyMemory<Color>? ColorTable { get; set; }

    /// <summary>
    /// Gets or sets the frame color table mode.
    /// </summary>
    public FrameColorTableMode ColorTableMode { get; set; }

    /// <summary>
    /// Gets or sets the duration of the frame.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets the frame alpha blending mode.
    /// </summary>
    public FrameBlendMode BlendMode { get; set; }

    /// <summary>
    /// Gets or sets the frame disposal mode.
    /// </summary>
    public FrameDisposalMode DisposalMode { get; set; }
}

#pragma warning disable SA1201 // Elements should appear in the correct order
internal enum FrameBlendMode
#pragma warning restore SA1201 // Elements should appear in the correct order
{
    /// <summary>
    /// Do not blend. Render the current frame on the canvas by overwriting the rectangle covered by the current frame.
    /// </summary>
    Source = 0,

    /// <summary>
    /// Blend the current frame with the previous frame in the animation sequence within the rectangle covered
    /// by the current frame.
    /// If the current has any transparent areas, the corresponding areas of the previous frame will be visible
    /// through these transparent regions.
    /// </summary>
    Over = 1
}

internal enum FrameDisposalMode
{
    /// <summary>
    /// No disposal specified.
    /// The decoder is not required to take any action.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Do not dispose. The current frame is not disposed of, or in other words, not cleared or altered when moving to
    /// the next frame. This means that the next frame is drawn over the current frame, and if the next frame contains
    /// transparency, the previous frame will be visible through these transparent areas.
    /// </summary>
    DoNotDispose = 1,

    /// <summary>
    /// Restore to background color. When transitioning to the next frame, the area occupied by the current frame is
    /// filled with the background color specified in the image metadata.
    /// This effectively erases the current frame by replacing it with the background color before the next frame is displayed.
    /// </summary>
    RestoreToBackground = 2,

    /// <summary>
    /// Restore to previous. This method restores the area affected by the current frame to what it was before the
    /// current frame was displayed. It essentially "undoes" the current frame, reverting to the state of the image
    /// before the frame was displayed, then the next frame is drawn. This is useful for animations where only a small
    /// part of the image changes from frame to frame.
    /// </summary>
    RestoreToPrevious = 3
}

internal enum FrameColorTableMode
{
    /// <summary>
    /// The frame uses the shared color table specified by the image metadata.
    /// </summary>
    Global,

    /// <summary>
    /// The frame uses a color table specified by the frame metadata.
    /// </summary>
    Local
}
