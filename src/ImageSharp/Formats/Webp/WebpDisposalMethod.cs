// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Indicates how the current frame is to be treated after it has been displayed (before rendering the next frame) on the canvas.
/// </summary>
public enum WebpDisposalMethod
{
    /// <summary>
    /// Do not dispose. Leave the canvas as is.
    /// </summary>
    None = 0,

    /// <summary>
    /// Dispose to background color. Fill the rectangle on the canvas covered by the current frame with background color specified in the ANIM chunk.
    /// </summary>
    RestoreToBackground = 1
}
