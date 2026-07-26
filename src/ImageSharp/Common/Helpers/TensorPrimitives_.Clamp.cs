// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Common.Helpers;

internal static partial class TensorPrimitives_
{
    /// <summary>
    /// Computes the element-wise result of clamping <paramref name="x"/> to the inclusive range specified
    /// by <paramref name="min"/> and <paramref name="max"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="x">The values to clamp.</param>
    /// <param name="min">The inclusive lower bound.</param>
    /// <param name="max">The inclusive upper bound.</param>
    /// <param name="destination">The destination for the clamped values.</param>
    /// <exception cref="ArgumentException"><paramref name="destination"/> is shorter than <paramref name="x"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> and <paramref name="destination"/> overlap without beginning at the same memory location.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clamp<T>(ReadOnlySpan<T> x, T min, T max, Span<T> destination)
        where T : INumber<T>
        => InvokeSpanScalarScalarIntoSpan<T, ClampOperator<T>>(x, min, max, destination);

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> ClampSingle(
        Vector128<float> value,
        Vector128<float> min,
        Vector128<float> max)
    {
        // Unlike the native x86 min/max instructions, the normalized runtime operations propagate a NaN in the
        // first operand and select negative zero when equal values have different signs.
        Vector128<float> maximum = Vector128.ConditionalSelect(
            Vector128.LessThan(min, value)
            | ~Vector128.Equals(value, value)
            | (Vector128.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector128.ConditionalSelect(
            Vector128.LessThan(maximum, max)
            | ~Vector128.Equals(maximum, maximum)
            | (Vector128.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<float> ClampSingle(
        Vector256<float> value,
        Vector256<float> min,
        Vector256<float> max)
    {
        Vector256<float> maximum = Vector256.ConditionalSelect(
            Vector256.LessThan(min, value)
            | ~Vector256.Equals(value, value)
            | (Vector256.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector256.ConditionalSelect(
            Vector256.LessThan(maximum, max)
            | ~Vector256.Equals(maximum, maximum)
            | (Vector256.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps single-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<float> ClampSingle(
        Vector512<float> value,
        Vector512<float> min,
        Vector512<float> max)
    {
        Vector512<float> maximum = Vector512.ConditionalSelect(
            Vector512.LessThan(min, value)
            | ~Vector512.Equals(value, value)
            | (Vector512.Equals(value, min) & (min.AsInt32() >> 31).AsSingle()),
            value,
            min);

        return Vector512.ConditionalSelect(
            Vector512.LessThan(maximum, max)
            | ~Vector512.Equals(maximum, maximum)
            | (Vector512.Equals(maximum, max) & (maximum.AsInt32() >> 31).AsSingle()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<double> ClampDouble(
        Vector128<double> value,
        Vector128<double> min,
        Vector128<double> max)
    {
        Vector128<double> maximum = Vector128.ConditionalSelect(
            Vector128.LessThan(min, value)
            | ~Vector128.Equals(value, value)
            | (Vector128.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector128.ConditionalSelect(
            Vector128.LessThan(maximum, max)
            | ~Vector128.Equals(maximum, maximum)
            | (Vector128.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<double> ClampDouble(
        Vector256<double> value,
        Vector256<double> min,
        Vector256<double> max)
    {
        Vector256<double> maximum = Vector256.ConditionalSelect(
            Vector256.LessThan(min, value)
            | ~Vector256.Equals(value, value)
            | (Vector256.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector256.ConditionalSelect(
            Vector256.LessThan(maximum, max)
            | ~Vector256.Equals(maximum, maximum)
            | (Vector256.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps double-precision values with the normalized runtime semantics.
    /// </summary>
    /// <param name="value">The values to clamp.</param>
    /// <param name="min">The inclusive lower bounds.</param>
    /// <param name="max">The inclusive upper bounds.</param>
    /// <returns>The clamped values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<double> ClampDouble(
        Vector512<double> value,
        Vector512<double> min,
        Vector512<double> max)
    {
        Vector512<double> maximum = Vector512.ConditionalSelect(
            Vector512.LessThan(min, value)
            | ~Vector512.Equals(value, value)
            | (Vector512.Equals(value, min) & (min.AsInt64() >> 63).AsDouble()),
            value,
            min);

        return Vector512.ConditionalSelect(
            Vector512.LessThan(maximum, max)
            | ~Vector512.Equals(maximum, maximum)
            | (Vector512.Equals(maximum, max) & (maximum.AsInt64() >> 63).AsDouble()),
            maximum,
            max);
    }

    /// <summary>
    /// Clamps values using the complete runtime tensor contract, including signed-zero correction.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    private readonly struct ClampOperator<T> : ITernaryOperator<T>
        where T : INumber<T>
    {
        /// <summary>
        /// Gets a value indicating whether this operation supports vector execution.
        /// </summary>
        public static bool Vectorizable => true;

        /// <summary>
        /// Clamps a scalar value.
        /// </summary>
        /// <param name="x">The value.</param>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The inclusive upper bound.</param>
        /// <returns>The clamped value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Invoke(T x, T min, T max)
            => Vector128<T>.IsSupported ? T.Min(T.Max(x, min), max) : T.Clamp(x, min, max);

        /// <summary>
        /// Clamps a 128-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> Invoke(Vector128<T> x, Vector128<T> min, Vector128<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector128<float> result = ClampSingle(
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref min),
                    Unsafe.As<Vector128<T>, Vector128<float>>(ref max));

                return Unsafe.As<Vector128<float>, Vector128<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector128<double> result = ClampDouble(
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref x),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref min),
                    Unsafe.As<Vector128<T>, Vector128<double>>(ref max));

                return Unsafe.As<Vector128<double>, Vector128<T>>(ref result);
            }

            return Vector128_.Clamp(x, min, max);
        }

        /// <summary>
        /// Clamps a 256-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> Invoke(Vector256<T> x, Vector256<T> min, Vector256<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector256<float> result = ClampSingle(
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref min),
                    Unsafe.As<Vector256<T>, Vector256<float>>(ref max));

                return Unsafe.As<Vector256<float>, Vector256<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector256<double> result = ClampDouble(
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref x),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref min),
                    Unsafe.As<Vector256<T>, Vector256<double>>(ref max));

                return Unsafe.As<Vector256<double>, Vector256<T>>(ref result);
            }

            return Vector256_.Clamp(x, min, max);
        }

        /// <summary>
        /// Clamps a 512-bit vector.
        /// </summary>
        /// <param name="x">The values.</param>
        /// <param name="min">The inclusive lower bounds.</param>
        /// <param name="max">The inclusive upper bounds.</param>
        /// <returns>The clamped values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> Invoke(Vector512<T> x, Vector512<T> min, Vector512<T> max)
        {
            if (typeof(T) == typeof(float))
            {
                Vector512<float> result = ClampSingle(
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref min),
                    Unsafe.As<Vector512<T>, Vector512<float>>(ref max));

                return Unsafe.As<Vector512<float>, Vector512<T>>(ref result);
            }

            if (typeof(T) == typeof(double))
            {
                Vector512<double> result = ClampDouble(
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref x),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref min),
                    Unsafe.As<Vector512<T>, Vector512<double>>(ref max));

                return Unsafe.As<Vector512<double>, Vector512<T>>(ref result);
            }

            return Vector512_.Clamp(x, min, max);
        }
    }
}
