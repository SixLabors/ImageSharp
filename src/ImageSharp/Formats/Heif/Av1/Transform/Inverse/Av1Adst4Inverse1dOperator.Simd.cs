// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Inverse;

/// <content>
/// Provides the SIMD kernels for the four-point inverse ADST operator.
/// </content>
internal readonly partial struct Av1Adst4Inverse1dOperator
{
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
        Vector128<int> x0 = input[0];
        Vector128<int> x1 = input[1];
        Vector128<int> x2 = input[2];
        Vector128<int> x3 = input[3];

        // The products retain the sine-table scale across the complete matrix. The bounded transform inputs make
        // the optimized kernels' wrapping 32-bit multiply/add sequence valid until the terminal rounding shift.
        output[0] = Av1Transform1dMath.MultiplyAdd4(sinpi[1], x0, sinpi[3], x1, sinpi[4], x2, sinpi[2], x3, cosBit);
        output[1] = Av1Transform1dMath.MultiplyAdd4(sinpi[2], x0, sinpi[3], x1, -sinpi[1], x2, -sinpi[4], x3, cosBit);
        output[2] = Av1Transform1dMath.MultiplyAdd4(sinpi[3], x0, 0, x1, -sinpi[3], x2, sinpi[3], x3, cosBit);
        output[3] = Av1Transform1dMath.MultiplyAdd4(sinpi[1] + sinpi[2], x0, -sinpi[3], x1, sinpi[4] - sinpi[1], x2, sinpi[2] - sinpi[4], x3, cosBit);
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
        Vector256<int> x0 = input[0];
        Vector256<int> x1 = input[1];
        Vector256<int> x2 = input[2];
        Vector256<int> x3 = input[3];

        output[0] = Av1Transform1dMath.MultiplyAdd4(sinpi[1], x0, sinpi[3], x1, sinpi[4], x2, sinpi[2], x3, cosBit);
        output[1] = Av1Transform1dMath.MultiplyAdd4(sinpi[2], x0, sinpi[3], x1, -sinpi[1], x2, -sinpi[4], x3, cosBit);
        output[2] = Av1Transform1dMath.MultiplyAdd4(sinpi[3], x0, 0, x1, -sinpi[3], x2, sinpi[3], x3, cosBit);
        output[3] = Av1Transform1dMath.MultiplyAdd4(sinpi[1] + sinpi[2], x0, -sinpi[3], x1, sinpi[4] - sinpi[1], x2, sinpi[2] - sinpi[4], x3, cosBit);
    }
}
