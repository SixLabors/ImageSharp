// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The way the encoder varies the quantizer between segments of a frame. Reference: AQ_MODE.
/// </summary>
internal enum Av1AdaptiveQuantizationMode
{
    /// <summary>
    /// Every block codes at the frame quantizer. Reference: NO_AQ.
    /// </summary>
    None = 0,

    /// <summary>
    /// Segments follow the source variance of each block. Reference: VARIANCE_AQ.
    /// </summary>
    Variance = 1,

    /// <summary>
    /// Segments follow the coded complexity of each block. Reference: COMPLEXITY_AQ.
    /// </summary>
    Complexity = 2,

    /// <summary>
    /// A rotating set of blocks codes at a lower quantizer. Reference: CYCLIC_REFRESH_AQ.
    /// </summary>
    CyclicRefresh = 3
}
