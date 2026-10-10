// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the AV1 sequence profile and its permitted bit-depth and chroma formats.
/// </summary>
internal enum ObuSequenceProfile : uint
{
    /// <summary>
    /// The Main profile.
    /// </summary>
    Main = 0,

    /// <summary>
    /// The High profile.
    /// </summary>
    High = 1,

    /// <summary>
    /// The Professional profile.
    /// </summary>
    Professional = 2,
}
