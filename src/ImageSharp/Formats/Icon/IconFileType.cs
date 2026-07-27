// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Identifies the type stored in an ICO or CUR directory header.
/// </summary>
internal enum IconFileType : ushort
{
    /// <summary>
    /// A Windows icon file.
    /// </summary>
    ICO = 1,

    /// <summary>
    /// A Windows cursor file.
    /// </summary>
    CUR = 2
}
