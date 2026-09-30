// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Ans;

/// <summary>
/// Method for LZ77 compression.
/// </summary>
internal enum JxlLz77Method : byte
{
    /// <summary>
    /// Do not use LZ77.
    /// </summary>
    None,

    /// <summary>
    /// Use Run Length Encoding
    /// </summary>
    Rle,

    /// <summary>
    /// LZ77 fast without runtime cost comparison
    /// </summary>
    Lz77b1w3f,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b3w3f,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b7w3f,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b15w3f,

    /// <summary>
    /// Slow LZ77
    /// </summary>
    Lz77b31w3f,

    /// <summary>
    /// Fast LZ77, but with runtime cost comparison. Almost always worse.
    /// </summary>
    Lz77b1w3t,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b3w3t,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b7w3t,

    /// <summary>
    /// LZ77
    /// </summary>
    Lz77b15w3t,

    /// <summary>
    /// Slow LZ77
    /// </summary>
    Lz77b31w3t,

    /// <summary>
    /// Optimal-matching LZ77 (fast).
    /// </summary>
    Optc1,

    /// <summary>
    /// Optional-matching LZ77.
    /// </summary>
    Optc3,

    /// <summary>
    /// Optional-matching LZ77.
    /// </summary>
    Optc8,

    /// <summary>
    /// Optional-matching LZ77 parsing big chain length.
    /// </summary>
    Optc256,
}
