// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal enum ObuChromoSamplePosition : byte
{
    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Horizontally co-located with luma(0, 0) sample, between two vertical samples.
    /// </summary>
    Vertical = 1,

    /// <summary>
    /// Co-located with luma(0, 0) sample
    /// </summary>
    Colocated = 2,
}
