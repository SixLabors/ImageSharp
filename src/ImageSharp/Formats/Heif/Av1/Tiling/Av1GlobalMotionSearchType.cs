// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Selects the references that the global motion search visits.
/// </summary>
internal enum Av1GlobalMotionSearchType
{
    /// <summary>
    /// The search visits every reference except LAST2, LAST3 and ALTREF2.
    /// </summary>
    SkipLast2Last3Alternate2,

    /// <summary>
    /// The search visits only the closest past reference and the closest future reference.
    /// </summary>
    ClosestReferencesOnly,

    /// <summary>
    /// The encoder does no global motion search.
    /// </summary>
    Disabled,
}
