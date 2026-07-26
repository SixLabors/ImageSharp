// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Computes the element-wise product of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The multiplicands.</param>
    /// <param name="y">The multiplier.</param>
    /// <param name="destination">The destination for the products.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="x"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="destination"/> overlap without beginning at the same memory location.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Multiply<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
        => InvokeSpanScalarIntoSpan<T, MultiplyOperator<T>>(x, y, destination);

    /// <summary>
    /// Multiplies corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct MultiplyOperator<T> : IBinaryOperator<T>
        where T : IMultiplyOperators<T, T, T>, IMultiplicativeIdentity<T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Multiplies scalar values.
        /// </summary>
        /// <param name="x">The multiplicand.</param>
        /// <param name="y">The multiplier.</param>
        /// <returns>The product.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x * y;

        /// <summary>
        /// Multiplies 128-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x * y;

        /// <summary>
        /// Multiplies 256-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x * y;

        /// <summary>
        /// Multiplies 512-bit vectors.
        /// </summary>
        /// <param name="x">The multiplicands.</param>
        /// <param name="y">The multipliers.</param>
        /// <returns>The products.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x * y;
    }
}
