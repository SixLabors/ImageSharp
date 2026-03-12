// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Provides a way to specify how the color table is used by the frame.
/// </summary>
public enum FrameColorTableMode
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
