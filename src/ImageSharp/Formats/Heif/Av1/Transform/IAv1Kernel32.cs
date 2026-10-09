// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines one thirty-two-point transform over thirty-two consecutive vectors whose sixteen lanes each hold one independent input set.
/// </summary>
internal interface IAv1Kernel32
{
    /// <summary>
    /// Transforms thirty-two inputs in place. Every lane is transformed on its own.
    /// </summary>
    /// <param name="data">The first of the thirty-two consecutive inputs and outputs.</param>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    public static abstract void Transform(ref Vector256<short> data, int cosBit);
}
