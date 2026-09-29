// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The delta quantizer mode of the encoder. Reference: DELTAQ_MODE.
/// </summary>
internal enum Av1DeltaQMode
{
    /// <summary>
    /// The frame quantizer applies to every superblock. Reference: NO_DELTA_Q.
    /// </summary>
    None,

    /// <summary>
    /// The quantizer of each superblock follows its importance in the temporal dependency model, when the model has
    /// statistics for the frame. Reference: DELTA_Q_OBJECTIVE.
    /// </summary>
    Objective,

    /// <summary>
    /// The quantizer of each superblock follows its variance. Reference: DELTA_Q_VARIANCE_BOOST.
    /// </summary>
    VarianceBoost,
}
