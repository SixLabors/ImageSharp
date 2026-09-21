// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Implements the four-tap cubic interpolation of the warped reference patch.
/// </content>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Applies a rounding right shift to one value.
    /// </summary>
    /// <param name="value">The value to shift.</param>
    /// <param name="bits">The shift, which is always one or more here.</param>
    /// <returns>The shifted value.</returns>
    /// <remarks>
    /// The rounding term is added before the arithmetic shift, which is what the reference macro
    /// ROUND_POWER_OF_TWO does for a signed value.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundShift(int value, int bits) => (value + (1 << (bits - 1))) >> bits;

    /// <summary>
    /// Applies a rounding right shift to thirty-two bit lanes.
    /// </summary>
    /// <param name="value">The lanes to shift.</param>
    /// <param name="bits">The shift, which is always one or more here.</param>
    /// <returns>The shifted lanes.</returns>
    /// <remarks>
    /// The shift is arithmetic, so a negative lane rounds towards negative infinity exactly as the
    /// scalar form does.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> RoundShift(Vector128<int> value, int bits)
        => (value + Vector128.Create(1 << (bits - 1))) >> bits;

    /// <summary>
    /// Weights four samples by the cubic taps and applies a rounded right shift.
    /// </summary>
    /// <remarks>
    /// Reference: the two interpolation passes of compute_flow_vector().
    /// </remarks>
    private readonly struct CubicOperator : IAv1CubicOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Filter(int s0, int s1, int s2, int s3, int k0, int k1, int k2, int k3, int roundBits)
        {
            int sum = (k0 * s0) + (k1 * s1) + (k2 * s2) + (k3 * s3);
            return RoundShift(sum, roundBits);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<int> Filter(
            Vector128<int> s0,
            Vector128<int> s1,
            Vector128<int> s2,
            Vector128<int> s3,
            Vector128<int> k0,
            Vector128<int> k1,
            Vector128<int> k2,
            Vector128<int> k3,
            int roundBits)
        {
            // Each lane is one independent output. The taps are broadcast by the traversal, so the
            // four products are plain lane-wise multiplications with no shuffle between them.
            // Thirty-two bit lanes are needed: a tap reaches a magnitude of about 18000 in Q14 and a
            // sample reaches 255, so one product alone passes the range of a sixteen-bit lane.
            Vector128<int> sum = (k0 * s0) + (k1 * s1) + (k2 * s2) + (k3 * s3);
            return RoundShift(sum, roundBits);
        }
    }
}
