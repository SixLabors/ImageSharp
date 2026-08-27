// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the AV1 sequence-header operating point selected by an AVIF image item.
/// </summary>
/// <param name="index">The zero-based operating-point index.</param>
internal readonly struct Av1OperatingPointSelector(byte index)
{
    /// <summary>
    /// Gets the zero-based operating-point index.
    /// </summary>
    public byte Index { get; } = index;
}
