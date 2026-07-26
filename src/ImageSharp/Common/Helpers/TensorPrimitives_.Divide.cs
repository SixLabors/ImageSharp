// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Computes the element-wise result of dividing the values in <paramref name="x"/> by <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The dividend values.</param>
    /// <param name="y">The divisor.</param>
    /// <param name="destination">The destination for the quotient values.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="x"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="destination"/> overlap without beginning at the same memory location.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Divide<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : IDivisionOperators<T, T, T>
        => InvokeSpanScalarIntoSpanForDivision<T, DivideOperator<T>>(x, y, destination);

    /// <summary>
    /// Determines whether <typeparamref name="T"/> has the same vector division support as <see cref="int"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <returns><see langword="true"/> when <typeparamref name="T"/> is a 32-bit signed native integer type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInt32Like<T>()
        => typeof(T) == typeof(int) || (IntPtr.Size == 4 && typeof(T) == typeof(nint));

    /// <summary>
    /// Divides values by a scalar.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct DivideOperator<T> : IBinaryOperator<T>
        where T : IDivisionOperators<T, T, T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => typeof(T) == typeof(float)
                                        || typeof(T) == typeof(double)
                                        || (Vector256.IsHardwareAccelerated && IsInt32Like<T>());

        /// <summary>
        /// Divides scalar values.
        /// </summary>
        /// <param name="x">The dividend.</param>
        /// <param name="y">The divisor.</param>
        /// <returns>The quotient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => x / y;

        /// <summary>
        /// Divides 128-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y) => x / y;

        /// <summary>
        /// Divides 256-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y) => x / y;

        /// <summary>
        /// Divides 512-bit vectors.
        /// </summary>
        /// <param name="x">The dividends.</param>
        /// <param name="y">The divisors.</param>
        /// <returns>The quotients.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y) => x / y;
    }
}
