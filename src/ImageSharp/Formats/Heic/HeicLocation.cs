// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Location within the file of an <see cref="HeicItem"/>.
/// </summary>
public class HeicLocation(long offset, long length)
{
    /// <summary>
    /// Gets the file offset of this location.
    /// </summary>
    public long Offset { get; } = offset;

    /// <summary>
    /// Gets the length of this location.
    /// </summary>
    public long Length { get; } = length;
}
