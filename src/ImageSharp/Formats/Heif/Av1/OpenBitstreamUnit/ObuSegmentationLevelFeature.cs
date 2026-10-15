// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies a feature that AV1 can configure independently for each segment.
/// </summary>
internal enum ObuSegmentationLevelFeature
{
    /// <summary>
    /// Adjusts the segment quantizer index.
    /// </summary>
    AlternativeQuantizer,

    /// <summary>
    /// Adjusts the vertical luma loop-filter level.
    /// </summary>
    AlternativeLoopFilterYVertical,

    /// <summary>
    /// Adjusts the horizontal luma loop-filter level.
    /// </summary>
    AlternativeLoopFilterYHorizontal,

    /// <summary>
    /// Adjusts the U-plane loop-filter level.
    /// </summary>
    AlternativeLoopFilterU,

    /// <summary>
    /// Adjusts the V-plane loop-filter level.
    /// </summary>
    AlternativeLoopFilterV,

    /// <summary>
    /// Selects a reference frame for the segment.
    /// </summary>
    ReferenceFrame,

    /// <summary>
    /// Marks every block in the segment as skipped.
    /// </summary>
    Skip,

    /// <summary>
    /// Uses the global motion vector for the segment.
    /// </summary>
    GlobalMotionVector,
}
