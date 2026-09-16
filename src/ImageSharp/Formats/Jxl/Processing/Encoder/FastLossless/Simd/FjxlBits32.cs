// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using static SharpImmintrin.CIntrinsicArmAdvSimd;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

/// <summary>
/// Contains a 32-bit pair of code lengths and code values, useful
/// for Huffman encoding.
/// </summary>
internal struct FjxlBits32(Vector<uint> counts, Vector<uint> bits)
{
    /// <summary>
    /// Gets or sets the code lengths.
    /// </summary>
    public Vector<uint> Counts { get; set; } = counts;

    /// <summary>
    /// Gets or sets the code values.
    /// </summary>
    public Vector<uint> Bits { get; set; } = bits;
}
