// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

/// <summary>
/// SIMD utilities highly specific to the Fast Lossless JXL encoder.
/// </summary>
internal static class FjxlSimdUtils
{
    public static Vector<short> HorizontalAdd(this Vector<short> a, Vector<short> b) => (a + b) >> 1;
}
