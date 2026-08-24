// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Hevc;

/// <summary>
/// Identifies the prediction structure signaled for an HEVC slice segment.
/// </summary>
internal enum HevcSliceType
{
    /// <summary>
    /// The slice can use intra and bidirectional inter prediction.
    /// </summary>
    Bidirectional = 0,

    /// <summary>
    /// The slice can use intra and forward inter prediction.
    /// </summary>
    Predictive = 1,

    /// <summary>
    /// The slice uses only intra-picture prediction.
    /// </summary>
    Intra = 2
}
