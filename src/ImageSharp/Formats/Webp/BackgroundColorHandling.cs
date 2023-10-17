// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Enum to decide how to handle the background color of the Animation chunk during decoding.
/// </summary>
public enum BackgroundColorHandling
{
    /// <summary>
    /// The background color of the ANIM chunk will be used to initialize the canvas to fill the unused space on the canvas around the frame.
    /// Also, if AnimationDisposalMethod.Dispose is used, this color will be used to restore the canvas background.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// The background color of the ANIM chunk is ignored and instead the canvas is initialized with transparent, BGRA(0, 0, 0, 0).
    /// </summary>
    Ignore = 1
}
