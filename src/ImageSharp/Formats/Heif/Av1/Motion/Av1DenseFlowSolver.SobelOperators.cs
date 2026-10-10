// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

/// <content>
/// Implements the two directions of the separable gradient filter.
/// </content>
internal static partial class Av1DenseFlowSolver
{
    /// <summary>
    /// Differentiates along the rows and smooths along the columns.
    /// </summary>
    /// <remarks>
    /// The derivative is <c>sample[-1] - sample[+1]</c>, which is minus twice the true derivative. The smoothing taps total four. Thus the pair
    /// carries the eight-fold scale of the gradients. The residual absorbs the sign.
    /// </remarks>
    private readonly struct HorizontalGradientOperator : IAv1SobelOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FilterRow(int left, int centre, int right) => left - right;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> FilterRow(Vector128<short> left, Vector128<short> centre, Vector128<short> right)
            => left - right;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FilterColumn(int above, int centre, int below) => above + centre + centre + below;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> FilterColumn(Vector128<short> above, Vector128<short> centre, Vector128<short> below)
        {
            // Each lane is one independent column. The code adds the centre twice and does not multiply it. An add is faster than a multiply on
            // every supported path, and both forms give the same wrapped result in sixteen-bit lanes.
            return above + centre + centre + below;
        }
    }

    /// <summary>
    /// Smooths along the rows and differentiates along the columns.
    /// </summary>
    /// <remarks>
    /// This operator uses the kernels of the horizontal gradient with the passes exchanged, so it carries the same eight-fold scale.
    /// </remarks>
    private readonly struct VerticalGradientOperator : IAv1SobelOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FilterRow(int left, int centre, int right) => left + centre + centre + right;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> FilterRow(Vector128<short> left, Vector128<short> centre, Vector128<short> right)
        {
            // Each lane is one independent column, as in the horizontal operator.
            return left + centre + centre + right;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FilterColumn(int above, int centre, int below) => above - below;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> FilterColumn(Vector128<short> above, Vector128<short> centre, Vector128<short> below)
            => above - below;
    }
}
