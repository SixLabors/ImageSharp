// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformSetType
{
    /// <summary>
    /// Allowed transforms: DCT only.
    /// </summary>
    DctOnly,

    /// <summary>
    /// Allowed transforms: DCT + Identity only
    /// </summary>
    InterSet3,

    /// <summary>
    /// Allowed transforms: Discrete Trig transforms w/o flip (4) + Identity (1)
    /// </summary>
    /// <remarks>Referenced in spec as TX_SET_INTRA_2.</remarks>
    IntraSet2,

    /// <summary>
    /// Allowed transforms: Discrete Trig transforms w/o flip (4) + Identity (1) + 1D Hor/vert DCT (2)
    /// </summary>
    /// <remarks>Referenced in spec as TX_SET_INTRA_1.</remarks>
    IntraSet1,

    /// <summary>
    /// Allowed transforms: Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver DCT (2)
    /// </summary>
    InterSet2,

    /// <summary>
    /// Allowed transforms: Discrete Trig transforms w/ flip (9) + Identity (1) + 1D Hor/Ver (6)
    /// </summary>
    InterSet1,
    AllSets
}
