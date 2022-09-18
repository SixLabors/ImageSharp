// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// The different pixel formats for a OpenEXR image.
/// </summary>
public enum ExrPixelType
{
    /// <summary>
    /// unsigned int (32 bit).
    /// </summary>
    UnsignedInt = 0,

    /// <summary>
    /// half (16 bit floating point).
    /// </summary>
    Half = 1,

    /// <summary>
    /// float (32 bit floating point).
    /// </summary>
    Float = 2
}
