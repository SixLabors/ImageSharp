// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformSetType
{
    /// <summary>
    /// DCT only.
    /// </summary>
    DctOnly,

    /// <summary>
    /// DCT + Identity only
    /// </summary>
    DctIdentity,

    /// <summary>
    /// Discrete Trig transforms w/o flip (4) + Identity (1)
    /// </summary>
    Dtt4Identity,

    /// <summary>
    /// Discrete Trig transforms w/o flip (4) + Identity (1) + 1D Hor/vert DCT (2)
    /// </summary>
    Dtt4Identity1dDct,

    /// <summary>
    /// Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver DCT (2)
    /// </summary>
    Dtt9Identity1dDct,

    /// <summary>
    /// Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver (6)
    /// </summary>
    All16
}
