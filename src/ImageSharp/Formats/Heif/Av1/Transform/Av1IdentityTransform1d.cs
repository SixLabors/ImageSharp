// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the shared SIMD arithmetic for AV1 identity-transform operators.
/// </summary>
/// <remarks>
/// Transform-vector fields represent positions along an identity transform and lanes represent independent axes.
/// Scaling is lane-local; a zero fractional-bit count uses exact multiplication, while fixed-point variants use the
/// same rounded butterfly primitive as DCT and ADST operators.
/// </remarks>
internal static class Av1IdentityTransform1d
{
    /// <summary>
    /// Scales four independent identity-transform axes in parallel.
    /// </summary>
    /// <param name="input">The source values for four transform axes.</param>
    /// <param name="output">The destination values for four transform axes.</param>
    /// <param name="length">The number of values in each axis.</param>
    /// <param name="multiplier">The fixed-point identity scale.</param>
    /// <param name="fractionalBits">The number of fractional bits in <paramref name="multiplier"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(ref Av1TransformVector<Vector128<int>> input, ref Av1TransformVector<Vector128<int>> output, int length, int multiplier, int fractionalBits)
    {
        if (fractionalBits == 0)
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] * multiplier;
            }

            return;
        }

        for (int i = 0; i < length; i++)
        {
            output[i] = Av1Transform1dMath.MultiplyRound(input[i], multiplier, fractionalBits);
        }
    }

    /// <summary>
    /// Scales eight independent identity-transform axes in parallel.
    /// </summary>
    /// <param name="input">The source values for eight transform axes.</param>
    /// <param name="output">The destination values for eight transform axes.</param>
    /// <param name="length">The number of values in each axis.</param>
    /// <param name="multiplier">The fixed-point identity scale.</param>
    /// <param name="fractionalBits">The number of fractional bits in <paramref name="multiplier"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(ref Av1TransformVector<Vector256<int>> input, ref Av1TransformVector<Vector256<int>> output, int length, int multiplier, int fractionalBits)
    {
        if (fractionalBits == 0)
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] * multiplier;
            }

            return;
        }

        for (int i = 0; i < length; i++)
        {
            output[i] = Av1Transform1dMath.MultiplyRound(input[i], multiplier, fractionalBits);
        }
    }

    /// <summary>
    /// Scales sixteen independent identity-transform axes in parallel.
    /// </summary>
    /// <param name="input">The source values for sixteen transform axes.</param>
    /// <param name="output">The destination values for sixteen transform axes.</param>
    /// <param name="length">The number of values in each axis.</param>
    /// <param name="multiplier">The fixed-point identity scale.</param>
    /// <param name="fractionalBits">The number of fractional bits in <paramref name="multiplier"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transform(ref Av1TransformVector<Vector512<int>> input, ref Av1TransformVector<Vector512<int>> output, int length, int multiplier, int fractionalBits)
    {
        if (fractionalBits == 0)
        {
            for (int i = 0; i < length; i++)
            {
                output[i] = input[i] * multiplier;
            }

            return;
        }

        for (int i = 0; i < length; i++)
        {
            output[i] = Av1Transform1dMath.MultiplyRound(input[i], multiplier, fractionalBits);
        }
    }
}
