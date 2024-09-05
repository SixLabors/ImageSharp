// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Implementation of a specific forward transform function.
/// </summary>
internal interface IAv1ForwardTransformer
{
    /// <summary>
    /// Execute the transformation.
    /// </summary>
    /// <param name="input">Input pixels.</param>
    /// <param name="output">Output coefficients.</param>
    /// <param name="cosBit">The cosinus bit.</param>
    /// <param name="stageRange">Stage ranges.</param>
    void Transform(ref int input, ref int output, int cosBit, Span<byte> stageRange);

    /// <summary>
    /// Execute the transformation using <see cref="Avx2"/> instructions.
    /// </summary>
    /// <param name="input">Array of input vectors.</param>
    /// <param name="output">Array of output coefficients vectors.</param>
    /// <param name="cosBit">The cosinus bit.</param>
    /// <param name="columnNumber">The column number to process.</param>
    void TransformAvx2(ref Vector256<int> input, ref Vector256<int> output, int cosBit, int columnNumber);
}
