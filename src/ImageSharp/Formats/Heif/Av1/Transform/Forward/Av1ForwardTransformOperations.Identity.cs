// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Implements the length-specific forward identity transform scaling.
/// </content>
internal static partial class Av1ForwardTransformOperations
{
    /// <summary>
    /// Applies the four-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The unused fixed-point precision.</param>
    public static void Identity4<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
        => Identity(ref input, ref output, ref step, cosBit, 4, 1, 0);

    /// <summary>
    /// Applies the eight-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The unused fixed-point precision.</param>
    public static void Identity8<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
        => Identity(ref input, ref output, ref step, cosBit, 8, 0, 1);

    /// <summary>
    /// Applies the sixteen-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The unused fixed-point precision.</param>
    public static void Identity16<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
        => Identity(ref input, ref output, ref step, cosBit, 16, 2, 0);

    /// <summary>
    /// Applies the thirty-two-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The unused fixed-point precision.</param>
    public static void Identity32<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit)
        where TValue : struct
        => Identity(ref input, ref output, ref step, cosBit, 32, 0, 2);

    /// <summary>
    /// Applies the length-specific AV1 identity scaling to every transform value.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="input">The spatial-domain values.</param>
    /// <param name="output">The frequency-domain values.</param>
    /// <param name="step">The unused transform-stage buffer.</param>
    /// <param name="cosBit">The unused fixed-point precision.</param>
    /// <param name="length">The number of transform values.</param>
    /// <param name="sqrt2Scale">The multiplier applied with the fixed-point square-root-of-two constant.</param>
    /// <param name="leftShift">The direct left shift applied when square-root scaling is not required.</param>
    private static void Identity<TValue>(
        ref Av1TransformVector<TValue> input,
        ref Av1TransformVector<TValue> output,
        ref Av1TransformVector<TValue> step,
        int cosBit,
        int length,
        int sqrt2Scale,
        int leftShift)
        where TValue : struct
    {
        _ = step;
        _ = cosBit;

        // AV1 defines identity normalization by transform length: 4 and 16 use NewSqrt2 scaling, while 8 and 32
        // are exact powers of two. The same operation applies independently to each SIMD lane.
        for (int i = 0; i < length; i++)
        {
            output[i] = sqrt2Scale != 0
                ? Av1ForwardTransformArithmetic<TValue>.MultiplyRound(
                    input[i],
                    sqrt2Scale * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits)
                : Av1ForwardTransformArithmetic<TValue>.ShiftLeft(input[i], leftShift);
        }
    }
}
