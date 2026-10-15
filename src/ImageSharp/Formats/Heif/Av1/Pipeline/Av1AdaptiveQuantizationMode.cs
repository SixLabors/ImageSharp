// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The way the encoder varies the quantizer between segments of a frame.
/// </summary>
internal enum Av1AdaptiveQuantizationMode
{
    /// <summary>
    /// Every block codes at the frame quantizer.
    /// </summary>
    None = 0,

    /// <summary>
    /// Segments follow the source variance of each block.
    /// </summary>
    Variance = 1,

    /// <summary>
    /// Segments follow the coded complexity of each block.
    /// </summary>
    Complexity = 2,

    /// <summary>
    /// A rotating set of blocks codes at a lower quantizer.
    /// </summary>
    CyclicRefresh = 3
}
