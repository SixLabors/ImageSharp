// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Specifies the format of the frame data.
/// </summary>
public enum AniFrameFormat : byte
{
    /// <summary>
    /// The frame resource is encoded as a Windows cursor.
    /// </summary>
    Cur,

    /// <summary>
    /// The frame resource is encoded as a Windows icon.
    /// </summary>
    Ico,

    /// <summary>
    /// The frame resource is encoded as a Windows bitmap.
    /// </summary>
    Bmp
}
