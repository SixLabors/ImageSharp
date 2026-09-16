// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

/// <summary>
/// Contains a 64-bit pair of code lengths and code values, useful
/// for Huffman encoding.
/// </summary>
internal struct FjxlBits64(Vector<ulong> counts, Vector<ulong> bits)
{
    public static int Lanes { get; } = Vector<ulong>.Count;

    /// <summary>
    /// Gets or sets the code lengths.
    /// </summary>
    public Vector<ulong> Counts { get; set; } = counts;

    /// <summary>
    /// Gets or sets the code values.
    /// </summary>
    public Vector<ulong> Bits { get; set; } = bits;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Store(ref ulong nbitsOut, ref ulong bitsOut)
    {
        this.Counts.StoreUnsafe(ref nbitsOut);
        this.Bits.StoreUnsafe(ref bitsOut);
    }
}
