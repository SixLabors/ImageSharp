// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies whether inter blocks use only single references or can select compound references.
/// </summary>
internal enum ObuReferenceMode
{
    /// <summary>
    /// Only single-reference prediction is permitted.
    /// </summary>
    SingleReference = 0,

    /// <summary>
    /// Each eligible block selects single- or compound-reference prediction.
    /// </summary>
    ReferenceModeSelect = 1,
}
