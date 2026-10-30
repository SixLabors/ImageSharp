// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Defines the type of a JPEG XL frame.
/// </summary>
internal enum JxlFrameType : byte
{
    /// <summary>
    /// A regular frame. It might be a crop, and it will be blended
    /// on a previous frame (if any) and likely displayed or blended in
    /// future frames.
    /// </summary>
    RegularFrame,

    /// <summary>
    /// A DC frame. It is downsampled and only used as the DC
    /// of a future and, possibly, preview frame. This cannot be cropped,
    /// blended, or referenced by patches or blending modes. Frames using
    /// DC cannot have non-default sizes.
    /// </summary>
    DcFrame,

    /// <summary>
    /// A PatchesSource frame. Can only be used as source frame for
    /// taking patches. It can be cropped but can't have a non-(0, 0) x0/y0.
    /// </summary>
    ReferenceOnly = 2,

    /// <summary>
    /// Same as regular frame but not used for progressive rendering.
    /// Implies no early display of DC.
    /// </summary>
    SkipProgressive,
}
