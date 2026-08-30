// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Identifies the supported image presentation declared by a HEIF file-type box.
/// </summary>
internal enum HeifFileType
{
    /// <summary>
    /// The file-type box does not declare a supported HEIF image presentation.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The container presents a primary image item.
    /// </summary>
    StillImage,

    /// <summary>
    /// The container presents a timed AVIF image sequence.
    /// </summary>
    ImageSequence
}
