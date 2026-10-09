// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies the set of transform combinations allowed for an AV1 block.
/// </summary>
internal enum Av1TransformSetType
{
    /// <summary>
    /// Allowed transforms: DCT only.
    /// </summary>
    DctOnly,

    /// <summary>
    /// Allowed transforms: DCT and identity only.
    /// </summary>
    InterSet3,

    /// <summary>
    /// Allowed transforms: the four DCT and ADST combinations without flip, and identity.
    /// </summary>
    /// <remarks>The AV1 specification calls this set TX_SET_INTRA_2.</remarks>
    IntraSet2,

    /// <summary>
    /// Allowed transforms: the four DCT and ADST combinations without flip, identity, and the horizontal and vertical one-dimensional DCT.
    /// </summary>
    /// <remarks>The AV1 specification calls this set TX_SET_INTRA_1.</remarks>
    IntraSet1,

    /// <summary>
    /// Allowed transforms: the nine DCT and ADST combinations with flip, identity, and the horizontal and vertical one-dimensional DCT.
    /// </summary>
    InterSet2,

    /// <summary>
    /// Allowed transforms: the nine DCT and ADST combinations with flip, identity, and all six horizontal and vertical one-dimensional transforms.
    /// </summary>
    InterSet1,

    /// <summary>
    /// The number of defined transform sets.
    /// </summary>
    AllSets
}
