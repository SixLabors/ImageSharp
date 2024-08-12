// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides a way to specify how the current frame should be blended with the previous frame in the animation sequence.
/// </summary>
public enum FrameBlendMode
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
