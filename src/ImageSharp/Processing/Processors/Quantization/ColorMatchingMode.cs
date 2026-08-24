// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Processing.Processors.Quantization;

/// <summary>
/// Defines the precision level used when matching colors during quantization.
/// </summary>
public enum ColorMatchingMode
{
    /// <summary>
    /// Uses a coarse caching strategy optimized for performance at the expense of exact matches.
    /// This provides the fastest matching but may yield approximate results.
    /// </summary>
    Coarse,

    /// <summary>
    /// Performs exact color matching using a bounded exact-match cache with eviction.
    /// This preserves exact color matching while accelerating repeated colors.
    /// </summary>
    Exact
}
