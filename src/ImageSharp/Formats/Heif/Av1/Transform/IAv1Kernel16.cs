// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Defines one sixteen-point transform over sixteen vectors of sixteen lanes, one input per vector.
/// </summary>
internal interface IAv1Kernel16
{
    /// <summary>
    /// Transforms sixteen inputs in place.
    /// </summary>
    /// <param name="in0">The first input and output.</param>
    /// <param name="in1">The second input and output.</param>
    /// <param name="in2">The third input and output.</param>
    /// <param name="in3">The fourth input and output.</param>
    /// <param name="in4">The fifth input and output.</param>
    /// <param name="in5">The sixth input and output.</param>
    /// <param name="in6">The seventh input and output.</param>
    /// <param name="in7">The eighth input and output.</param>
    /// <param name="in8">The ninth input and output.</param>
    /// <param name="in9">The tenth input and output.</param>
    /// <param name="in10">The eleventh input and output.</param>
    /// <param name="in11">The twelfth input and output.</param>
    /// <param name="in12">The thirteenth input and output.</param>
    /// <param name="in13">The fourteenth input and output.</param>
    /// <param name="in14">The fifteenth input and output.</param>
    /// <param name="in15">The sixteenth input and output.</param>
    /// <param name="cosBit">The number of fractional bits in the transform constants.</param>
    public static abstract void Transform(
        ref Vector256<short> in0,
        ref Vector256<short> in1,
        ref Vector256<short> in2,
        ref Vector256<short> in3,
        ref Vector256<short> in4,
        ref Vector256<short> in5,
        ref Vector256<short> in6,
        ref Vector256<short> in7,
        ref Vector256<short> in8,
        ref Vector256<short> in9,
        ref Vector256<short> in10,
        ref Vector256<short> in11,
        ref Vector256<short> in12,
        ref Vector256<short> in13,
        ref Vector256<short> in14,
        ref Vector256<short> in15,
        int cosBit);
}
