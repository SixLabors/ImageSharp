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
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Identity4<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
        => Identity(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit, 4, 1, 0);

    /// <summary>
    /// Applies the eight-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Identity8<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
        => Identity(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit, 8, 0, 1);

    /// <summary>
    /// Applies the sixteen-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Identity16<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
        => Identity(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit, 16, 2, 0);

    /// <summary>
    /// Applies the thirty-two-point forward identity transform to every independent lane.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    public static void Identity32<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit)
        where TValue : struct
        => Identity(ref values, inputStride, outputStride, ref buffer0, ref buffer1, cosBit, 32, 0, 2);

    /// <summary>
    /// Applies the length-specific AV1 identity scaling directly to the strided transform block.
    /// </summary>
    /// <typeparam name="TValue">The scalar or SIMD value containing the independent transform axes.</typeparam>
    /// <param name="values">The first value in the strided transform block.</param>
    /// <param name="inputStride">The byte distance between consecutive input positions.</param>
    /// <param name="outputStride">The byte distance between consecutive output positions.</param>
    /// <param name="buffer0">The first fixed transform-stage buffer.</param>
    /// <param name="buffer1">The second fixed transform-stage buffer.</param>
    /// <param name="cosBit">The fixed-point precision of the cosine constants.</param>
    /// <param name="length">The number of transform positions.</param>
    /// <param name="sqrt2Scale">The square-root-of-two multiplier, or zero when power-of-two scaling applies.</param>
    /// <param name="leftShift">The power-of-two scaling shift.</param>
    private static void Identity<TValue>(
        ref byte values,
        nint inputStride,
        nint outputStride,
        ref Av1TransformVector<TValue> buffer0,
        ref Av1TransformVector<TValue> buffer1,
        int cosBit,
        int length,
        int sqrt2Scale,
        int leftShift)
        where TValue : struct
    {
        _ = buffer0;
        _ = buffer1;
        _ = cosBit;

        // AV1 defines identity normalization by transform length: 4 and 16 use sqrt(2) scaling, while 8 and 32
        // are exact powers of two. Applying it in place matches Highway's row-oriented identity kernels.
        for (int i = 0; i < length; i++)
        {
            TValue input = Load<TValue>(ref values, inputStride, i);
            TValue output = sqrt2Scale != 0
                ? Av1ForwardTransformArithmetic<TValue>.MultiplyRound(
                    input,
                    sqrt2Scale * Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits)
                : Av1ForwardTransformArithmetic<TValue>.ShiftLeft(input, leftShift);

            Store(ref values, outputStride, i, output);
        }
    }
}
