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
    /// The derivative is <c>sample[-1] - sample[+1]</c>, which is minus twice the true derivative,
    /// and the smoothing taps total four. The pair therefore carries the eight-fold scale that the
    /// gradients are defined to have, and the sign is absorbed by the residual.
    /// Reference: sobel_filter() with a direction of one.
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
            // Each lane is one independent column. The centre is added twice rather than multiplied,
            // because an add is cheaper than a multiply on every supported path and the two forms
            // give the same wrapped result in sixteen-bit lanes.
            return above + centre + centre + below;
        }
    }

    /// <summary>
    /// Smooths along the rows and differentiates along the columns.
    /// </summary>
    /// <remarks>
    /// This is the same pair of kernels as the horizontal gradient with the passes exchanged, so it
    /// carries the same eight-fold scale. Reference: sobel_filter() with a direction of zero.
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
