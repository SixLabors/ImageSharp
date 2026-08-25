// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Enumerates the component bit depths supported for HEIF image encoding.
/// </summary>
public enum HeifBitDepth : byte
{
    /// <summary>
    /// Eight bits per image component.
    /// </summary>
    Bit8 = 8,

    /// <summary>
    /// Ten bits per image component.
    /// </summary>
    Bit10 = 10,

    /// <summary>
    /// Twelve bits per image component.
    /// </summary>
    Bit12 = 12
}
