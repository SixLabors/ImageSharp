// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the orientation of an AV1 difference-weighted compound mask.
/// </summary>
internal enum Av1DifferenceWeightedMaskType : byte
{
    /// <summary>
    /// Applies the predictor-difference adjustment to a base alpha weight of 38 on AV1's 0-through-64 blend scale.
    /// </summary>
    Type38 = 0,

    /// <summary>
    /// Applies the complement of the type-38 predictor-difference mask.
    /// </summary>
    Type38Inverse = 1,
}
