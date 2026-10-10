// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines one eight-point transform over eight vectors, one input per vector.
/// </summary>
internal interface IAv1Kernel8
{
    /// <summary>
    /// Transforms eight inputs in place.
    /// </summary>
    /// <param name="in0">The first input and output.</param>
    /// <param name="in1">The second input and output.</param>
    /// <param name="in2">The third input and output.</param>
    /// <param name="in3">The fourth input and output.</param>
    /// <param name="in4">The fifth input and output.</param>
    /// <param name="in5">The sixth input and output.</param>
    /// <param name="in6">The seventh input and output.</param>
    /// <param name="in7">The eighth input and output.</param>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    public static abstract void Transform(
        ref Vector128<short> in0,
        ref Vector128<short> in1,
        ref Vector128<short> in2,
        ref Vector128<short> in3,
        ref Vector128<short> in4,
        ref Vector128<short> in5,
        ref Vector128<short> in6,
        ref Vector128<short> in7,
        int cosBit);
}
