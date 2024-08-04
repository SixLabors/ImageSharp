// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides a way to specify how the current frame should be disposed of before rendering the next frame.
/// </summary>
public enum FrameDisposalMode
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
