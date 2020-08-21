// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Text;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a number that can be expressed as a fraction.
    /// </summary>
    /// <remarks>
    /// This is a very simplified implementation of a rational number designed for use with metadata only.
    /// </remarks>
    internal readonly struct LongRational : IEquatable<LongRational>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LongRational"/> struct.
        /// </summary>
        /// <param name="numerator">
        /// The number above the line in a vulgar fraction showing how many of the parts
        /// indicated by the denominator are taken.
        /// </param>
        /// <param name="denominator">
        /// The number below the line in a vulgar fraction; a divisor.
        /// </param>
        public LongRational(long numerator, long denominator)
        {
            this.Numerator = numerator;
            this.Denominator = denominator;
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public long Numerator { get; }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public long Denominator { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is indeterminate.
        /// </summary>
        public bool IsIndeterminate => this.Denominator == 0 && this.Numerator == 0;

        /// <summary>
        /// Gets a value indicating whether this instance is an integer (n, 1)
        /// </summary>
        public bool IsInteger => this.Denominator == 1;

        /// <summary>
        /// Gets a value indicating whether this instance is equal to negative infinity (-1, 0)
        /// </summary>
        public bool IsNegativeInfinity => this.Denominator == 0 && this.Numerator == -1;

        /// <summary>
        /// Gets a value indicating whether this instance is equal to positive infinity (1, 0)
        /// </summary>
        public bool IsPositiveInfinity => this.Denominator == 0 && this.Numerator == 1;

        /// <summary>
        /// Gets a value indicating whether this instance is equal to 0 (0, 1)
        /// </summary>
        public bool IsZero => this.Denominator == 1 && this.Numerator == 0;

        /// <inheritdoc/>
        public bool Equals(LongRational other)
        {
            return this.Numerator == other.Numerator && this.Denominator == other.Denominator;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return ((this.Numerator * 397) ^ this.Denominator).GetHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Converts the numeric value of this instance to its equivalent string representation using
        /// the specified culture-specific format information.
        /// </summary>
        /// <param name="provider">
        /// An object that supplies culture-specific formatting information.
        /// </param>
        /// <returns>The <see cref="string"/></returns>
        public string ToString(IFormatProvider provider)
        {
            if (this.IsIndeterminate)
            {
                return "[ Indeterminate ]";
            }

            if (this.IsPositiveInfinity)
            {
                return "[ PositiveInfinity ]";
            }

            if (this.IsNegativeInfinity)
            {
                return "[ NegativeInfinity ]";
            }

            if (this.IsZero)
            {
                return "0";
            }

            if (this.IsInteger)
            {
                return this.Numerator.ToString(provider);
            }

            var sb = new StringBuilder();
            sb.Append(this.Numerator.ToString(provider));
            sb.Append('/');
            sb.Append(this.Denominator.ToString(provider));
            return sb.ToString();
        }

        /// <summary>
        /// Create a new instance of the <see cref="LongRational"/> struct from a double value.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        public static LongRational FromDouble(double value, bool bestPrecision)
        {
            if (double.IsNaN(value))
            {
                return new LongRational(0, 0);
            }

            if (double.IsPositiveInfinity(value))
            {
                return new LongRational(1, 0);
            }

            if (double.IsNegativeInfinity(value))
            {
                return new LongRational(-1, 0);
            }

            long numerator = 1;
            long denominator = 1;

            double val = Math.Abs(value);
            double df = numerator / (double)denominator;
            double epsilon = bestPrecision ? double.Epsilon : .000001;

            while (Math.Abs(df - val) > epsilon)
            {
                if (df < val)
                {
                    numerator++;
                }
                else
                {
                    denominator++;
                    numerator = (int)(val * denominator);
                }

                df = numerator / (double)denominator;
            }

            if (value < 0.0)
            {
                numerator *= -1;
            }

            return new LongRational(numerator, denominator).Simplify();
        }

        /// <summary>
        /// Finds the greatest common divisor of two <see cref="long"/> values.
        /// </summary>
        /// <param name="left">The first value</param>
        /// <param name="right">The second value</param>
        /// <returns>The <see cref="long"/></returns>
        private static long GreatestCommonDivisor(long left, long right)
        {
            return right == 0 ? left : GreatestCommonDivisor(right, left % right);
        }

        /// <summary>
        /// Simplifies the <see cref="LongRational"/>
        /// </summary>
        public LongRational Simplify()
        {
            if (this.IsIndeterminate ||
                this.IsNegativeInfinity ||
                this.IsPositiveInfinity ||
                this.IsInteger ||
                this.IsZero)
            {
                return this;
            }

            if (this.Numerator == 0)
            {
                return new LongRational(0, 0);
            }

            if (this.Numerator == this.Denominator)
            {
                return new LongRational(1, 1);
            }

            long gcd = GreatestCommonDivisor(Math.Abs(this.Numerator), Math.Abs(this.Denominator));

            if (gcd > 1)
            {
                return new LongRational(this.Numerator / gcd, this.Denominator / gcd);
            }

            return this;
        }
    }
}