// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <summary>
/// Identifies the neighboring sample regions required by an AV1 intra-prediction mode.
/// </summary>
[Flags]
internal enum Av1NeighborNeed
{
    /// <summary>
    /// No neighboring samples are required.
    /// </summary>
    Nothing = 0,

    /// <summary>
    /// Samples immediately left of the block are required.
    /// </summary>
    Left = 2,

    /// <summary>
    /// Samples immediately above the block are required.
    /// </summary>
    Above = 4,

    /// <summary>
    /// Samples extending right of the top edge are required.
    /// </summary>
    AboveRight = 8,

    /// <summary>
    /// The sample diagonally above and left of the block is required.
    /// </summary>
    AboveLeft = 16,

    /// <summary>
    /// Samples extending below the left edge are required.
    /// </summary>
    BottomLeft = 32,
}
