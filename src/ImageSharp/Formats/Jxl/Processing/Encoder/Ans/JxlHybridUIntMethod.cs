// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// Hybrid uint method.
/// </summary>
internal enum JxlHybridUIntMethod : byte
{
    /// <summary>
    /// Simply use HybridUint420Configuration
    /// </summary>
    None,

    /// <summary>
    /// Force the fastest option.
    /// </summary>
    Method000,

    /// <summary>
    /// Try a couple of options.
    /// </summary>
    Fast,

    /// <summary>
    /// Fast choice for context maps.
    /// </summary>
    ContextMap,

    /// <summary>
    /// Slowest.
    /// </summary>
    Best,
}
