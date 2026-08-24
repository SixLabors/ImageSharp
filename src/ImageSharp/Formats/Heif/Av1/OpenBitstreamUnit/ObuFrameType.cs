// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the AV1 frame type signaled by a frame header.
/// </summary>
internal enum ObuFrameType
{
    /// <summary>
    /// A key frame that is decoded without reference to another frame.
    /// </summary>
    KeyFrame = 0,

    /// <summary>
    /// An inter frame that can refer to previously decoded frames.
    /// </summary>
    InterFrame = 1,

    /// <summary>
    /// An intra-only frame that does not refresh all reference slots.
    /// </summary>
    IntraOnlyFrame = 2,

    /// <summary>
    /// A switch frame that permits switching between coded sequences.
    /// </summary>
    SwitchFrame = 3,
}
