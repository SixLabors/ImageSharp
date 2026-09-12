// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

/// <summary>
/// Status of processing a section.
/// </summary>
internal enum JxlSectionStatus : byte
{
    /// <summary>
    /// Processed normally.
    /// </summary>
    Done,

    /// <summary>
    /// Skipped because other required sections were not yet processed.
    /// </summary>
    Skipped,

    /// <summary>
    /// Skipped because the section was already processed.
    /// </summary>
    Duplicate,

    /// <summary>
    /// Only partially decoded. Section will be processed again.
    /// </summary>
    Partial
}
