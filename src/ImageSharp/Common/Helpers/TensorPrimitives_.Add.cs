// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Computes the element-wise sum of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The first addends.</param>
    /// <param name="y">The second addends.</param>
    /// <param name="destination">The destination for the sums.</param>
    /// <exception cref="ArgumentException"><paramref name="x"/> and <paramref name="y"/> do not have the same length.</exception>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than the input spans.</exception>
    /// <exception cref="ArgumentException">
    /// An input and <paramref name="destination"/> overlap without beginning at the same memory location.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(ReadOnlySpan<T> x, ReadOnlySpan<T> y, Span<T> destination)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => InvokeSpanSpanIntoSpan<T, AddOperator<T>>(x, y, destination);

    /// <summary>
    /// Computes the element-wise sum of the values in <paramref name="x"/> and the scalar <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The first addends.</param>
    /// <param name="y">The scalar second addend.</param>
    /// <param name="destination">The destination for the sums.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="x"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="destination"/> overlap without beginning at the same memory location.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Add<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
        => InvokeSpanScalarIntoSpan<T, AddOperator<T>>(x, y, destination);

    /// <summary>
    /// Adds corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct AddOperator<T> : IBinaryOperator<T>
        where T : IAdditionOperators<T, T, T>, IAdditiveIdentity<T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Adds scalar values.
        /// </summary>
        /// <param name="x">The first addend.</param>
        /// <param name="y">The second addend.</param>
        /// <returns>The sum.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x + y;

        /// <summary>
        /// Adds 128-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x + y;

        /// <summary>
        /// Adds 256-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x + y;

        /// <summary>
        /// Adds 512-bit vectors.
        /// </summary>
        /// <param name="x">The first addends.</param>
        /// <param name="y">The second addends.</param>
        /// <returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x + y;
    }
}
