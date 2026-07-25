// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Computes the element-wise maximum of the values in <paramref name="x"/> and <paramref name="y"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The values to compare.</param>
    /// <param name="y">The value to compare with each element.</param>
    /// <param name="destination">The destination for the maximum values.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Max<T>(ReadOnlySpan<T> x, T y, Span<T> destination)
        where T : INumber<T>
        => InvokeSpanScalarIntoSpan<T, MaxOperator<T>>(x, y, destination);

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> MaxSingle(Vector128<float> x, Vector128<float> y)
    {
        // The .NET 8 operation already handles ordered unequal values. Correct its second-operand result for a
        // first-operand NaN, then use bitwise AND for equal values so positive zero wins regardless of operand order.
        Vector128<float> result = Vector128.Max(x, y);
        result = Vector128.ConditionalSelect(~Vector128.Equals(x, x), x, result);

        return Vector128.ConditionalSelect(
            Vector128.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> MaxSingle(Vector256<float> x, Vector256<float> y)
    {
        Vector256<float> result = Vector256.Max(x, y);
        result = Vector256.ConditionalSelect(~Vector256.Equals(x, x), x, result);

        return Vector256.ConditionalSelect(
            Vector256.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> MaxSingle(Vector512<float> x, Vector512<float> y)
    {
        Vector512<float> result = Vector512.Max(x, y);
        result = Vector512.ConditionalSelect(~Vector512.Equals(x, x), x, result);

        return Vector512.ConditionalSelect(
            Vector512.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> MaxDouble(Vector128<double> x, Vector128<double> y)
    {
        Vector128<double> result = Vector128.Max(x, y);
        result = Vector128.ConditionalSelect(~Vector128.Equals(x, x), x, result);

        return Vector128.ConditionalSelect(
            Vector128.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> MaxDouble(Vector256<double> x, Vector256<double> y)
    {
        Vector256<double> result = Vector256.Max(x, y);
        result = Vector256.ConditionalSelect(~Vector256.Equals(x, x), x, result);

        return Vector256.ConditionalSelect(
            Vector256.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects maximum double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="x">The first values.</param>
    /// <param name="y">The second values.</param>
    /// <returns>The maximum values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> MaxDouble(Vector512<double> x, Vector512<double> y)
    {
        Vector512<double> result = Vector512.Max(x, y);
        result = Vector512.ConditionalSelect(~Vector512.Equals(x, x), x, result);

        return Vector512.ConditionalSelect(
            Vector512.Equals(x, y),
            x & y,
            result);
    }

    /// <summary>
    /// Selects the maximum corresponding values.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct MaxOperator<T> : IBinaryOperator<T>
        where T : INumber<T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Selects the maximum scalar value.
        /// </summary>
        /// <param name="x">The first value.</param>
        /// <param name="y">The second value.</param>
        /// <returns>The maximum value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T y) => T.Max(x, y);

        /// <summary>
        /// Selects the maximum values from 128-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector128<float> result = MaxSingle(
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref y));

                return Unsafe.As<Vector128<float>, Vector128<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector128<double> result = MaxDouble(
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref y));

                return Unsafe.As<Vector128<double>, Vector128<T>>(ref result);
            }

            return Vector128.Max(x, y);
        }

        /// <summary>
        /// Selects the maximum values from 256-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector256<float> result = MaxSingle(
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref y));

                return Unsafe.As<Vector256<float>, Vector256<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector256<double> result = MaxDouble(
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref y));

                return Unsafe.As<Vector256<double>, Vector256<T>>(ref result);
            }

            return Vector256.Max(x, y);
        }

        /// <summary>
        /// Selects the maximum values from 512-bit vectors.
        /// </summary>
        /// <param name="x">The first values.</param>
        /// <param name="y">The second values.</param>
        /// <returns>The maximum values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> y)
        {
            if (typeof(T) == typeof(float))
            {
                Vector512<float> result = MaxSingle(
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref y));

                return Unsafe.As<Vector512<float>, Vector512<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector512<double> result = MaxDouble(
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref y));

                return Unsafe.As<Vector512<double>, Vector512<T>>(ref result);
            }

            return Vector512.Max(x, y);
        }
    }
}
