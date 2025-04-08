// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Flags for the ANI header.
/// </summary>
[Flags]
public enum AniHeaderFlags : uint
{
    /// <summary>
    /// If set, the ANI file's "icon" chunk contains an ICO or CUR file, otherwise it contains a BMP file.
    /// </summary>
    IsIcon = 1,

    /// <summary>
    /// If set, the ANI file contains a "seq " chunk.
    /// </summary>
    ContainsSeq = 2
}
