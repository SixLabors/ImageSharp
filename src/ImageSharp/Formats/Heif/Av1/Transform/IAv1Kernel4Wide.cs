// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines one four-point transform over four vectors whose eight lanes each hold one independent input set.
/// </summary>
internal interface IAv1Kernel4Wide
{
    /// <summary>
    /// Transforms four inputs in place. Every lane is meaningful and is transformed on its own.
    /// </summary>
    /// <param name="in0">The first input and output.</param>
    /// <param name="in1">The second input and output.</param>
    /// <param name="in2">The third input and output.</param>
    /// <param name="in3">The fourth input and output.</param>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    public static abstract void Transform(
        ref Vector128<short> in0,
        ref Vector128<short> in1,
        ref Vector128<short> in2,
        ref Vector128<short> in3,
        int cosBit);
}
