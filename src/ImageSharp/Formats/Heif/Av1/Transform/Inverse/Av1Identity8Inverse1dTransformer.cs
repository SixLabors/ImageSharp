// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Applies the eight-point AV1 inverse identity transform to a one-dimensional coefficient vector.
/// </summary>
internal class Av1Identity8Inverse1dTransformer : IAv1Transformer1d
{
    /// <inheritdoc/>
    public void Transform(Span<int> input, Span<int> output, int cosBit, Span<byte> stageRange)
    {
        Guard.MustBeSizedAtLeast(input, 8, nameof(input));
        Guard.MustBeSizedAtLeast(output, 8, nameof(output));
        TransformScalar(ref input[0], ref output[0]);
    }

    /// <summary>
    /// Scales eight coefficients according to the AV1 inverse identity-transform definition.
    /// </summary>
    /// <param name="input">A reference to the first input coefficient.</param>
    /// <param name="output">A reference to the first output value.</param>
    /// <remarks>Corresponds to <c>svt_av1_iidentity8_c</c> in the original WIP reference.</remarks>
    private static void TransformScalar(ref int input, ref int output)
    {
        output = input << 1;
        Unsafe.Add(ref output, 1) = Unsafe.Add(ref input, 1) << 1;
        Unsafe.Add(ref output, 2) = Unsafe.Add(ref input, 2) << 1;
        Unsafe.Add(ref output, 3) = Unsafe.Add(ref input, 3) << 1;
        Unsafe.Add(ref output, 4) = Unsafe.Add(ref input, 4) << 1;
        Unsafe.Add(ref output, 5) = Unsafe.Add(ref input, 5) << 1;
        Unsafe.Add(ref output, 6) = Unsafe.Add(ref input, 6) << 1;
        Unsafe.Add(ref output, 7) = Unsafe.Add(ref input, 7) << 1;
    }
}
