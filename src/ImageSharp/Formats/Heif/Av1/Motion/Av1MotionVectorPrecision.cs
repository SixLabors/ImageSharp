// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Identifies the fractional precision used to decode an AV1 motion-vector delta.
/// </summary>
internal enum Av1MotionVectorPrecision : sbyte
{
    /// <summary>
    /// Restricts components to whole-sample increments.
    /// </summary>
    Integer = -1,

    /// <summary>
    /// Allows components in quarter-sample increments.
    /// </summary>
    QuarterSample,

    /// <summary>
    /// Allows components in eighth-sample increments.
    /// </summary>
    EighthSample
}
