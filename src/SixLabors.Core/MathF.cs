// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors
{
    /// <summary>
    /// Provides single-precision floating point constants and static methods for trigonometric, logarithmic, and other common mathematical functions.
    /// </summary>
    // ReSharper disable InconsistentNaming
    internal static class MathF
    {
        /// <summary>
        /// Represents the ratio of the circumference of a circle to its diameter, specified by the constant, π.
        /// </summary>
        public const float PI = (float)Math.PI;

        /// <summary>
        /// Returns the absolute value of a single-precision floating-point number.
        /// </summary>
        /// <param name="f">
        /// A number that is greater than or equal to <see cref="F:System.Single.MinValue" />, but less than or equal to <see cref="F:System.Single.MaxValue" />.
        /// </param>
        /// <returns>
        /// A single-precision floating-point number, x, such that 0 ≤ x ≤<see cref="F:System.Single.MaxValue" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Abs(float f)
        {
            return Math.Abs(f);
        }

        /// <summary>
        /// Returns the angle whose tangent is the quotient of two specified numbers.
        /// </summary>
        /// <param name="y">The y coordinate of a point.</param>
        /// <param name="x">The x coordinate of a point.</param>
        /// <returns>
        /// An angle, θ, measured in radians, such that -π≤θ≤π, and tan(θ) = y / x, where
        /// (x, y) is a point in the Cartesian plane. Observe the following: For (x, y) in
        /// quadrant 1, 0 &lt; θ &lt; π/2.For (x, y) in quadrant 2, π/2 &lt; θ≤π.For (x, y) in quadrant
        /// 3, -π &lt; θ &lt; -π/2.For (x, y) in quadrant 4, -π/2 &lt; θ &lt; 0.For points on the boundaries
        /// of the quadrants, the return value is the following:If y is 0 and x is not negative,
        /// θ = 0.If y is 0 and x is negative, θ = π.If y is positive and x is 0, θ = π/2.If
        /// y is negative and x is 0, θ = -π/2.If y is 0 and x is 0, θ = 0. If x or y is
        /// <see cref="F:System.Single.NaN"/>, or if x and y are either <see cref="F:System.Single.PositiveInfinity"/> or
        /// <see cref="F:System.Single.NegativeInfinity"/>, the method returns <see cref="F:System.Single.NaN"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Atan2(float y, float x)
        {
            return (float)Math.Atan2(y, x);
        }

        /// <summary>
        /// Returns the smallest integral value that is greater than or equal to the specified single-precision floating-point number.
        /// </summary>
        /// <param name="f">A single-precision floating-point number.</param>
        /// <returns>
        /// The smallest integral value that is greater than or equal to <paramref name="f" />.
        /// If <paramref name="f" /> is equal to <see cref="F:System.Single.NaN" />, <see cref="F:System.Single.NegativeInfinity" />,
        /// or <see cref="F:System.Single.PositiveInfinity" />, that value is returned.
        /// Note that this method returns a <see cref="T:System.Single" /> instead of an integral type.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Ceiling(float f)
        {
            return (float)Math.Ceiling(f);
        }

        /// <summary>
        /// Returns the cosine of the specified angle.
        /// </summary>
        /// <param name="f">An angle, measured in radians.</param>
        /// <returns>
        /// The cosine of <paramref name="f"/>. If <paramref name="f"/> is equal to <see cref="F:System.Float.NaN"/>, <see cref="F:System.Float.NegativeInfinity"/>,
        /// or <see cref="F:System.Float.PositiveInfinity"/>, this method returns <see cref="F:System.Float.NaN"/>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float f)
        {
            return (float)Math.Cos(f);
        }

        /// <summary>
        /// Converts a degree (360-periodic) angle to a radian (2*Pi-periodic) angle.
        /// </summary>
        /// <param name="degree">The angle in degrees.</param>
        /// <returns>
        /// The <see cref="float"/> representing the degree as radians.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToRadians(float degree)
        {
            return degree * (PI / 180F);
        }

        /// <summary>
        /// Returns e raised to the specified power.
        /// </summary>
        /// <param name="f">A number specifying a power.</param>
        /// <returns>
        /// The number e raised to the power <paramref name="f" />.
        /// If <paramref name="f" /> equals <see cref="F:System.Single.NaN" /> or <see cref="F:System.Single.PositiveInfinity" />, that value is returned.
        /// If <paramref name="f" /> equals <see cref="F:System.Single.NegativeInfinity" />, 0 is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Exp(float f)
        {
            return (float)Math.Exp(f);
        }

        /// <summary>
        /// Returns the largest integer less than or equal to the specified single-precision floating-point number.
        /// </summary>
        /// <param name="f">A single-precision floating-point number. </param>
        /// <returns>
        /// The largest integer less than or equal to <paramref name="f" />.
        /// If <paramref name="f" /> is equal to <see cref="F:System.Single.NaN" />, <see cref="F:System.Single.NegativeInfinity" />,
        /// or <see cref="F:System.Single.PositiveInfinity" />, that value is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Floor(float f)
        {
            return (float)Math.Floor(f);
        }

        /// <summary>
        /// Returns the larger of two single-precision floating-point numbers.
        /// </summary>
        /// <param name="val1">The first of two single-precision floating-point numbers to compare. </param>
        /// <param name="val2">The second of two single-precision floating-point numbers to compare. </param>
        /// <returns>
        /// Parameter <paramref name="val1" /> or <paramref name="val2" />, whichever is larger.
        /// If <paramref name="val1" />, or <paramref name="val2" />, or both <paramref name="val1" /> and <paramref name="val2" /> are
        /// equal to <see cref="F:System.Single.NaN" />, <see cref="F:System.Single.NaN" /> is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Max(float val1, float val2)
        {
            return Math.Max(val1, val2);
        }

        /// <summary>
        /// Returns the smaller of two single-precision floating-point numbers.
        /// </summary>
        /// <param name="val1">The first of two single-precision floating-point numbers to compare. </param>
        /// <param name="val2">The second of two single-precision floating-point numbers to compare. </param>
        /// <returns>
        /// Parameter <paramref name="val1" /> or <paramref name="val2" />, whichever is smaller.
        /// If <paramref name="val1" />, <paramref name="val2" />, or both <paramref name="val1" /> and <paramref name="val2" /> are equal
        /// to <see cref="F:System.Single.NaN" />, <see cref="F:System.Single.NaN" /> is returned.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Min(float val1, float val2)
        {
            return Math.Min(val1, val2);
        }

        /// <summary>
        /// Returns a specified number raised to the specified power.
        /// </summary>
        /// <param name="x">A single-precision floating-point number to be raised to a power. </param>
        /// <param name="y">A single-precision floating-point number that specifies a power. </param>
        /// <returns>The number <paramref name="x" /> raised to the power <paramref name="y" />.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow(float x, float y)
        {
            return (float)Math.Pow(x, y);
        }

        /// <summary>
        /// Converts a radian (2*Pi-periodic) angle to a degree (360-periodic) angle.
        /// </summary>
        /// <param name="radian">The angle in radians.</param>
        /// <returns>
        /// The <see cref="float"/> representing the degree as radians.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToDegree(float radian)
        {
            return radian / (PI / 180F);
        }

        /// <summary>
        /// Rounds a single-precision floating-point value to the nearest integral value.
        /// </summary>
        /// <param name="f">A single-precision floating-point number to be rounded.</param>
        /// <returns>
        /// The integer nearest <paramref name="f" />.
        /// If the fractional component of <paramref name="f" /> is halfway between two integers, one of which is even and the other odd, then the even number is returned.
        /// Note that this method returns a <see cref="T:System.Single" /> instead of an integral type.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float f)
        {
            return (float)Math.Round(f);
        }

        /// <summary>
        /// Rounds a single-precision floating-point value to the nearest integer.
        /// A parameter specifies how to round the value if it is midway between two numbers.
        /// </summary>
        /// <param name="f">A single-precision floating-point number to be rounded. </param>
        /// <param name="mode">Specification for how to round <paramref name="f" /> if it is midway between two other numbers.</param>
        /// <returns>
        /// The integer nearest <paramref name="f" />. If <paramref name="f" /> is halfway between two integers, one of which is even
        /// and the other odd, then <paramref name="mode" /> determines which of the two is returned.
        /// Note that this method returns a <see cref="T:System.Single" /> instead of an integral type.
        /// </returns>
        /// <exception cref="T:System.ArgumentException">
        /// <paramref name="mode" /> is not a valid value of <see cref="T:System.MidpointRounding" />.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Round(float f, MidpointRounding mode)
        {
            return (float)Math.Round(f, mode);
        }

        /// <summary>
        /// Returns the sine of the specified angle.
        /// </summary>
        /// <param name="f">An angle, measured in radians.</param>
        /// <returns>
        /// The sine of <paramref name="f" />.
        /// If <paramref name="f" /> is equal to <see cref="F:System.Single.NaN" />, <see cref="F:System.Single.NegativeInfinity" />,
        /// or <see cref="F:System.Single.PositiveInfinity" />, this method returns <see cref="F:System.Single.NaN" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float f)
        {
            return (float)Math.Sin(f);
        }

        /// <summary>
        /// Returns the result of a normalized sine cardinal function for the given value.
        /// SinC(x) = sin(pi*x)/(pi*x).
        /// </summary>
        /// <param name="f">A single-precision floating-point number to calculate the result for.</param>
        /// <returns>
        /// The sine cardinal of <paramref name="f" />.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SinC(float f)
        {
            if (Abs(f) > Constants.Epsilon)
            {
                f *= PI;
                return Clean(Sin(f) / f);
            }

            return 1F;
        }

        /// <summary>
        /// Returns the square root of a specified number.
        /// </summary>
        /// <param name="f">The number whose square root is to be found.</param>
        /// <returns>
        /// One of the values in the following table.
        /// <paramref name="f" /> parameter Return value Zero or positive The positive square root of <paramref name="f" />.
        /// Negative <see cref="F:System.Single.NaN" />Equals <see cref="F:System.Single.NaN" />
        /// <see cref="F:System.Single.NaN" />Equals <see cref="F:System.Single.PositiveInfinity" />
        /// <see cref="F:System.Single.PositiveInfinity" />
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sqrt(float f)
        {
            return (float)Math.Sqrt(f);
        }

        /// <summary>
        /// Ensures that any passed float is correctly rounded to zero
        /// </summary>
        /// <param name="x">The value to clean.</param>
        /// <returns>
        /// The <see cref="float"/>
        /// </returns>.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Clean(float x)
        {
            if (Abs(x) < Constants.Epsilon)
            {
                return 0F;
            }

            return x;
        }
    }
}