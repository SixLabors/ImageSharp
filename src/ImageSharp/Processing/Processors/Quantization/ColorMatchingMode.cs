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
    /// Enables an exact color match cache for the first 512 unique colors encountered,
    /// falling back to coarse matching thereafter.
    /// </summary>
    Hybrid,

    /// <summary>
    /// Performs exact color matching without any caching optimizations.
    /// This is the slowest but most accurate matching strategy.
    /// </summary>
    Exact
}
