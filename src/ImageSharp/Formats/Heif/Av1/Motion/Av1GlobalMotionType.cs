// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <summary>
/// Identifies the geometric model carried by AV1 global-motion parameters.
/// </summary>
internal enum Av1GlobalMotionType : byte
{
    /// <summary>
    /// The model has no geometric displacement.
    /// </summary>
    Identity = 0,

    /// <summary>
    /// The model has horizontal and vertical translation.
    /// </summary>
    Translation = 1,

    /// <summary>
    /// The model has translation, rotation, and uniform zoom.
    /// </summary>
    RotationZoom = 2,

    /// <summary>
    /// The model is a general six-parameter affine transformation.
    /// </summary>
    Affine = 3
}
