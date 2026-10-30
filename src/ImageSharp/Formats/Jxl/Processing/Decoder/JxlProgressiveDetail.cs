// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Types of progressive detail.
/// </summary>
internal enum JxlProgressiveDetail : byte
{
    /// <summary>
    /// After completed regular frames
    /// </summary>
    Frames,

    /// <summary>
    /// After completed DC.
    /// </summary>
    Dc,

    /// <summary>
    /// After completed AC passes that are the last pass for their
    /// resolution target.
    /// </summary>
    LastPasses,

    /// <summary>
    /// After completed AC passes that are not the last pass for their
    /// resolution target.
    /// </summary>
    Passes,

    /// <summary>
    /// During DC frame when lower resolution are completed.
    /// </summary>
    DcProgressive,

    /// <summary>
    /// After completed groups.
    /// </summary>
    DcGroups,

    /// <summary>
    /// After completed groups.
    /// </summary>
    Groups,
}
