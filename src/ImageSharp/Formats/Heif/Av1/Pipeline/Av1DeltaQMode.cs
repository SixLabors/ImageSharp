// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The delta quantizer mode of the encoder.
/// </summary>
internal enum Av1DeltaQMode
{
    /// <summary>
    /// The frame quantizer applies to every superblock.
    /// </summary>
    None,

    /// <summary>
    /// The quantizer of each superblock follows its importance in the temporal dependency model, when the model has statistics for the frame.
    /// </summary>
    Objective,

    /// <summary>
    /// The quantizer of each superblock follows its variance.
    /// </summary>
    VarianceBoost,
}
