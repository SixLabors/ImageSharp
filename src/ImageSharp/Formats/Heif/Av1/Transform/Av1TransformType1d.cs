// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies the one-dimensional transform applied along one axis of an AV1 transform block.
/// </summary>
internal enum Av1TransformType1d
{
    /// <summary>
    /// A discrete cosine transform.
    /// </summary>
    Dct,

    /// <summary>
    /// An asymmetric discrete sine transform.
    /// </summary>
    Adst,

    /// <summary>
    /// A flipped asymmetric discrete sine transform.
    /// </summary>
    FlipAdst,

    /// <summary>
    /// An identity transform.
    /// </summary>
    Identity
}
