// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Transforms;

/// <summary>
/// Represents the type of a transform.
/// </summary>
internal enum JxlTransformType : byte
{
    /// <summary>
    /// Reversible Color Transform
    /// </summary>
    Rct,

    /// <summary>
    /// Palette/indexed coding
    /// </summary>
    Palette,

    /// <summary>
    /// Haar-style squeezing
    /// </summary>
    Squeeze,

    /// <summary>
    /// Invalid/unknown
    /// </summary>
    Invalid
}
