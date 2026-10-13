// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the motion model used to construct an AV1 inter predictor.
/// </summary>
internal enum Av1MotionMode : byte
{
    /// <summary>
    /// Uses translational motion compensation without neighboring-block overlap.
    /// </summary>
    SimpleTranslation = 0,

    /// <summary>
    /// Blends the block with predictions derived from overlapping above and left neighbors.
    /// </summary>
    Obmc = 1,

    /// <summary>
    /// Uses a locally derived warped-motion model.
    /// </summary>
    Warped = 2,
}
