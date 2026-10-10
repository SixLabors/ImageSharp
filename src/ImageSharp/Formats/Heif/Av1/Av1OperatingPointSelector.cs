// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the AV1 sequence-header operating point selected by an AVIF image item.
/// </summary>
internal readonly struct Av1OperatingPointSelector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1OperatingPointSelector"/> struct.
    /// </summary>
    /// <param name="index">The zero-based operating-point index.</param>
    public Av1OperatingPointSelector(byte index)
    {
        this.Index = index;
    }

    /// <summary>
    /// Gets the zero-based operating-point index.
    /// </summary>
    public byte Index { get; }
}
