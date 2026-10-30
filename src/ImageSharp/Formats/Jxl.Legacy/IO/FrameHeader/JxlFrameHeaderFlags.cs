// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.IO.FrameHeader;

/// <summary>
/// Optional steps for postprocessing. These flags are the
/// source of truth. Override must set/clear them rather than
/// change their meaning. Values chosen such that typical flags
/// are 0, encoded in only two bits.
/// </summary>
[Flags]
internal enum JxlFrameHeaderFlags : byte
{
    /// <summary>
    /// Noise is injected into decoded output.
    /// </summary>
    Noise = 1,

    /// <summary>
    /// Overlay patches.
    /// </summary>
    Patches = 2,

    /// <summary>
    /// Overlay splines.
    /// </summary>
    Splines = 16,

    /// <summary>
    /// Implies skip adaptive DC smoothing.
    /// </summary>
    Dc = 32,

    SkipAdaptiveDcSmoothing = 128,
}
