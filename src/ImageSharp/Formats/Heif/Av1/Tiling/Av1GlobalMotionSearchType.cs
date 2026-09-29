// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Selects the references that the global motion search visits. Reference: GM_SEARCH_TYPE.
/// </summary>
internal enum Av1GlobalMotionSearchType
{
    /// <summary>
    /// Every reference. Reference: GM_FULL_SEARCH.
    /// </summary>
    Full,

    /// <summary>
    /// Every reference except LAST2 and LAST3. Reference: GM_REDUCED_REF_SEARCH_SKIP_L2_L3.
    /// </summary>
    SkipLast2Last3,

    /// <summary>
    /// Every reference except LAST2, LAST3 and ALTREF2. Reference: GM_REDUCED_REF_SEARCH_SKIP_L2_L3_ARF2.
    /// </summary>
    SkipLast2Last3Alternate2,

    /// <summary>
    /// The closest past and future references only. Reference: GM_SEARCH_CLOSEST_REFS_ONLY.
    /// </summary>
    ClosestReferencesOnly,

    /// <summary>
    /// No search. Reference: GM_DISABLE_SEARCH.
    /// </summary>
    Disabled,
}
