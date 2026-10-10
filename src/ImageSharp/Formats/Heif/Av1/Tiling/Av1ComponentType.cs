// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the luma or chroma component class used by AV1 entropy contexts.
/// </summary>
internal enum Av1ComponentType
{
    /// <summary>
    /// The luma component.
    /// </summary>
    Luminance = 0,

    /// <summary>
    /// Both chroma components.
    /// </summary>
    Chroma = 1,

    /// <summary>
    /// The blue-difference chroma component.
    /// </summary>
    ChromaCb = 2,

    /// <summary>
    /// The red-difference chroma component.
    /// </summary>
    ChromaCr = 3,

    /// <summary>
    /// The luma and both chroma components.
    /// </summary>
    All = 4,

    /// <summary>
    /// No component.
    /// </summary>
    None = 15
}
