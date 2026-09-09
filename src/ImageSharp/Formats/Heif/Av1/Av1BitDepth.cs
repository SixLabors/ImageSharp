// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Identifies the sample precision of an AV1 sequence.
/// </summary>
internal enum Av1BitDepth : int
{
    /// <summary>
    /// Eight bits per sample.
    /// </summary>
    EightBit = 0,

    /// <summary>
    /// Ten bits per sample.
    /// </summary>
    TenBit = 1,

    /// <summary>
    /// Twelve bits per sample.
    /// </summary>
    TwelveBit = 2,
}
