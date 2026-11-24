// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Compression.Ccitt;

/// <summary>
/// Defines the order of the CCITT code bits in each byte of the compressed data.
/// </summary>
internal enum CcittBitOrder
{
    /// <summary>
    /// The first code bit is the most significant bit of the byte.
    /// </summary>
    MostSignificantBitFirst = 0,

    /// <summary>
    /// The first code bit is the least significant bit of the byte.
    /// </summary>
    LeastSignificantBitFirst = 1
}
