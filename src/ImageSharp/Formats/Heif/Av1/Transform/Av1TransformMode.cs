// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies how transform-block sizes are selected within an AV1 frame.
/// </summary>
internal enum Av1TransformMode : byte
{
    /// <summary>
    /// Every transform block is four by four samples.
    /// </summary>
    Only4x4 = 0,

    /// <summary>
    /// Each block uses the largest permitted transform size.
    /// </summary>
    Largest = 1,

    /// <summary>
    /// Transform-block sizes are selected by block-level syntax.
    /// </summary>
    Select = 2,
}
