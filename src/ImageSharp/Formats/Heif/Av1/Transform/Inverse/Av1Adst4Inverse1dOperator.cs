// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <summary>
/// Defines the four-point AV1 inverse asymmetric discrete sine transform operator.
/// </summary>
/// <remarks>
/// Vector fields represent transform positions and vector lanes represent independent axes. The SIMD overloads apply
/// the same staged rotations, fixed-point rounding, and range clamps as the scalar overload without mixing axes.
/// </remarks>
internal readonly partial struct Av1Adst4Inverse1dOperator : IAv1Transform1dOperator
{
    /// <summary>
    /// Applies the normative four-point AV1 inverse asymmetric discrete sine transform.
    /// </summary>
    /// <param name="input">The four frequency-domain coefficients.</param>
    /// <param name="output">The four spatial-domain residual values.</param>
    /// <param name="step">The stage buffer owned by the containing two-dimensional transform.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    /// <param name="stageRange">The signed-bit range assigned to each transform stage.</param>
    public static void Transform(ReadOnlySpan<int> input, Span<int> output, Span<int> step, int cosBit, Av1TransformStageRange stageRange)
    {
        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);

        // libaom widens the complete four-point factorization because the products retain their fixed-point scale
        // until the final shift. The stage buffer is therefore unnecessary for this transform size.
        long x0 = input[0];
        long x1 = input[1];
        long x2 = input[2];
        long x3 = input[3];

        _ = step;
        _ = stageRange;

        // Avoid the multiplications for the all-zero coefficient vector, matching libaom's scalar kernel.
        if ((x0 | x1 | x2 | x3) == 0)
        {
            output[..4].Clear();
            return;
        }

        // Stages 1 and 2 form the seven sine products and the one unscaled combination used by stage 3.
        long s0 = sinpi[1] * x0;
        long s1 = sinpi[2] * x0;
        long s2 = sinpi[3] * x1;
        long s3 = sinpi[4] * x2;
        long s4 = sinpi[1] * x2;
        long s5 = sinpi[2] * x3;
        long s6 = sinpi[4] * x3;
        long s7 = (x0 - x2) + x3;

        // Stages 3 through 6 combine the products while preserving the fixed-point scale until the final rounding.
        s0 += s3;
        s1 -= s4;
        s3 = s2;
        s2 = sinpi[3] * s7;
        s0 += s5;
        s1 -= s6;
        x0 = s0 + s3;
        x1 = s1 + s3;
        x2 = s2;
        x3 = (s0 + s1) - s3;

        output[0] = Av1Math.RoundShift(x0, cosBit);
        output[1] = Av1Math.RoundShift(x1, cosBit);
        output[2] = Av1Math.RoundShift(x2, cosBit);
        output[3] = Av1Math.RoundShift(x3, cosBit);
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        TransformCore(ref input, ref output, cosBit);
        _ = step;
        _ = stageRange;
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector256<int>> input,
        ref Av1TransformVector<Vector256<int>> output,
        ref Av1TransformVector<Vector256<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        TransformCore(ref input, ref output, cosBit);
        _ = step;
        _ = stageRange;
    }

    /// <summary>
    /// Applies the inverse four-point matrix to four independent axes.
    /// </summary>
    /// <param name="input">The source values for four transform axes.</param>
    /// <param name="output">The destination values for four transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    private static void TransformCore(ref Av1TransformVector<Vector128<int>> input, ref Av1TransformVector<Vector128<int>> output, int cosBit)
    {
        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
        Vector128<int> x0 = input.V0;
        Vector128<int> x1 = input.V1;
        Vector128<int> x2 = input.V2;
        Vector128<int> x3 = input.V3;

        // The products retain the sine-table scale across the complete matrix. The bounded transform inputs make
        // the optimized kernels' wrapping 32-bit multiply/add sequence valid until the terminal rounding shift.
        output.V0 = Av1Transform1dMath.MultiplyAdd4(sinpi[1], x0, sinpi[3], x1, sinpi[4], x2, sinpi[2], x3, cosBit);
        output.V1 = Av1Transform1dMath.MultiplyAdd4(sinpi[2], x0, sinpi[3], x1, -sinpi[1], x2, -sinpi[4], x3, cosBit);
        output.V2 = Av1Transform1dMath.MultiplyAdd4(sinpi[3], x0, 0, x1, -sinpi[3], x2, sinpi[3], x3, cosBit);
        output.V3 = Av1Transform1dMath.MultiplyAdd4(sinpi[1] + sinpi[2], x0, -sinpi[3], x1, sinpi[4] - sinpi[1], x2, sinpi[2] - sinpi[4], x3, cosBit);
    }

    /// <summary>
    /// Applies the inverse four-point matrix to eight independent axes.
    /// </summary>
    /// <param name="input">The source values for eight transform axes.</param>
    /// <param name="output">The destination values for eight transform axes.</param>
    /// <param name="cosBit">The fixed-point precision of the sine constants.</param>
    private static void TransformCore(ref Av1TransformVector<Vector256<int>> input, ref Av1TransformVector<Vector256<int>> output, int cosBit)
    {
        ReadOnlySpan<int> sinpi = Av1SinusConstants.SinusPi(cosBit);
        Vector256<int> x0 = input.V0;
        Vector256<int> x1 = input.V1;
        Vector256<int> x2 = input.V2;
        Vector256<int> x3 = input.V3;

        output.V0 = Av1Transform1dMath.MultiplyAdd4(sinpi[1], x0, sinpi[3], x1, sinpi[4], x2, sinpi[2], x3, cosBit);
        output.V1 = Av1Transform1dMath.MultiplyAdd4(sinpi[2], x0, sinpi[3], x1, -sinpi[1], x2, -sinpi[4], x3, cosBit);
        output.V2 = Av1Transform1dMath.MultiplyAdd4(sinpi[3], x0, 0, x1, -sinpi[3], x2, sinpi[3], x3, cosBit);
        output.V3 = Av1Transform1dMath.MultiplyAdd4(sinpi[1] + sinpi[2], x0, -sinpi[3], x1, sinpi[4] - sinpi[1], x2, sinpi[2] - sinpi[4], x3, cosBit);
    }
}
