// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.FastLossless.Simd;

/// <summary>
/// Utilities for 16-bit SIMD vectors.
/// </summary>
internal static class FjxlSimdVec16
{
    public static int Lanes { get; } = Vector<short>.Count;
}
