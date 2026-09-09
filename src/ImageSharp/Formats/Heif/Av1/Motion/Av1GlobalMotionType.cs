// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Identifies the geometric model carried by AV1 global-motion parameters.
/// </summary>
internal enum Av1GlobalMotionType : byte
{
    /// <summary>
    /// No geometric displacement is applied.
    /// </summary>
    Identity = 0,

    /// <summary>
    /// Horizontal and vertical translation are applied.
    /// </summary>
    Translation = 1,

    /// <summary>
    /// Translation, rotation, and uniform zoom are applied.
    /// </summary>
    RotationZoom = 2,

    /// <summary>
    /// A general six-parameter affine transformation is applied.
    /// </summary>
    Affine = 3
}
