// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform.Forward;

/// <content>
/// Provides the SIMD kernels for the four-point forward identity-transform operator.
/// </content>
internal readonly partial struct Av1Identity4Forward1dOperator
{
    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
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
        Av1IdentityTransform1d.Transform(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector512<int>> input,
        ref Av1TransformVector<Vector512<int>> output,
        ref Av1TransformVector<Vector512<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 4, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }
}

/// <content>
/// Provides the SIMD kernels for the eight-point forward identity-transform operator.
/// </content>
internal readonly partial struct Av1Identity8Forward1dOperator
{
    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 8, 2, 0);
        _ = step;
        _ = cosBit;
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
        Av1IdentityTransform1d.Transform(ref input, ref output, 8, 2, 0);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector512<int>> input,
        ref Av1TransformVector<Vector512<int>> output,
        ref Av1TransformVector<Vector512<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 8, 2, 0);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }
}

/// <content>
/// Provides the SIMD kernels for the sixteen-point forward identity-transform operator.
/// </content>
internal readonly partial struct Av1Identity16Forward1dOperator
{
    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
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
        Av1IdentityTransform1d.Transform(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector512<int>> input,
        ref Av1TransformVector<Vector512<int>> output,
        ref Av1TransformVector<Vector512<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 16, 2 * Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }
}

/// <content>
/// Provides the SIMD kernels for the thirty-two-point forward identity-transform operator.
/// </content>
internal readonly partial struct Av1Identity32Forward1dOperator
{
    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector128<int>> input,
        ref Av1TransformVector<Vector128<int>> output,
        ref Av1TransformVector<Vector128<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 32, 4, 0);
        _ = step;
        _ = cosBit;
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
        Av1IdentityTransform1d.Transform(ref input, ref output, 32, 4, 0);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }

    /// <inheritdoc/>
    public static void Transform(
        ref Av1TransformVector<Vector512<int>> input,
        ref Av1TransformVector<Vector512<int>> output,
        ref Av1TransformVector<Vector512<int>> step,
        int cosBit,
        Av1TransformStageRange stageRange)
    {
        Av1IdentityTransform1d.Transform(ref input, ref output, 32, 4, 0);
        _ = step;
        _ = cosBit;
        _ = stageRange;
    }
}
