// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the search stage that selects transform and coefficient evaluation settings.
/// </summary>
internal enum Av1EncoderEvaluationStage
{
    /// <summary>
    /// Evaluate modes without a later winner-refinement stage.
    /// </summary>
    Default,

    /// <summary>
    /// Evaluate preliminary candidates using the configured search restrictions.
    /// </summary>
    Candidate,

    /// <summary>
    /// Refine retained candidates using the winner settings.
    /// </summary>
    Winner
}
