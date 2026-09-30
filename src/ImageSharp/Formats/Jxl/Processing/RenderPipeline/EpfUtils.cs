// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

/// <summary>
/// Utilities for EPF stages.
/// </summary>
internal static class EpfUtils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Weight(Vector256<float> sad, Vector256<float> inverseSigma)
    {
        Vector256<float> v = (sad * inverseSigma) + Vector256<float>.One;
        Vector256<float> whereNegative = Vector256.LessThan(v, Vector256<float>.Zero);
        Vector256<float> zeroIfNegative = Vector256.ConditionalSelect(whereNegative, Vector256<float>.Zero, whereNegative);
        return zeroIfNegative;
    }
}
