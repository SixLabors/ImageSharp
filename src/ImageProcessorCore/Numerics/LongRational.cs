// <copyright file="LongRational.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Represents a number that can be expressed as a fraction
    /// </summary>
    /// <remarks>
    /// This is a very simplified implementation of a rational number designed for use with metadata only.
    /// </remarks>
    internal struct LongRational : IEquatable<LongRational>
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
          : this(numerator, denominator, false)
        {
        }

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
        /// <param name="simplify">
        /// Whether to attempt to simplify the fractional parts.
        /// </param>
        public LongRational(long numerator, long denominator, bool simplify)
            : this()
        {
            this.Numerator = numerator;
            this.Denominator = denominator;

            if (simplify)
            {
                this.Simplify();
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LongRational"/> struct. 
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        public LongRational(double value, bool bestPrecision)
            : this()
        {
            if (double.IsNaN(value))
            {
                this.Numerator = this.Denominator = 0;
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                this.Numerator = 1;
                this.Denominator = 0;
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                this.Numerator = -1;
                this.Denominator = 0;
                return;
            }

            this.Numerator = 1;
            this.Denominator = 1;

            double val = Math.Abs(value);
            double df = this.Numerator / (double)this.Denominator;
            double epsilon = bestPrecision ? double.Epsilon : .000001;

            while (Math.Abs(df - val) > epsilon)
            {
                if (df < val)
                {
                    this.Numerator++;
                }
                else
                {
                    this.Denominator++;
                    this.Numerator = (int)(val * this.Denominator);
                }

                df = this.Numerator / (double)this.Denominator;
            }

            if (value < 0.0)
            {
                this.Numerator *= -1;
            }

            this.Simplify();
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public long Numerator
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public long Denominator
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a value indicating whether this instance is indeterminate. 
        /// </summary>
        public bool IsIndeterminate
        {
            get
            {
                if (this.Denominator != 0)
                {
                    return false;
                }

                return this.Numerator == 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is an integer (n, 1)
        /// </summary>
        public bool IsInteger => this.Denominator == 1;

        /// <summary>
        /// Gets a value indicating whether this instance is equal to negative infinity (-1, 0) 
        /// </summary>
        public bool IsNegativeInfinity
        {
            get
            {
                if (this.Denominator != 0)
                {
                    return false;
                }

                return this.Numerator == -1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equal to positive infinity (1, 0) 
        /// </summary>
        public bool IsPositiveInfinity
        {
            get
            {
                if (this.Denominator != 0)
                {
                    return false;
                }

                return this.Numerator == 1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equal to 0 (0, 1)
        /// </summary>
        public bool IsZero
        {
            get
            {
                if (this.Denominator != 1)
                {
                    return false;
                }

                return this.Numerator == 0;
            }
        }

        /// <inheritdoc/>
        public bool Equals(LongRational other)
        {
            if (this.Denominator == other.Denominator)
            {
                return this.Numerator == other.Numerator;
            }

            if (this.Numerator == 0 && this.Denominator == 0)
            {
                return other.Numerator == 0 && other.Denominator == 0;
            }

            if (other.Numerator == 0 && other.Denominator == 0)
            {
                return this.Numerator == 0 && this.Denominator == 0;
            }

            return (this.Numerator * other.Denominator) == (this.Denominator * other.Numerator);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return this.GetHashCode(this);
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

            StringBuilder sb = new StringBuilder();
            sb.Append(this.Numerator.ToString("R", provider));
            sb.Append("/");
            sb.Append(this.Denominator.ToString("R", provider));
            return sb.ToString();
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
        private void Simplify()
        {
            if (this.IsIndeterminate)
            {
                return;
            }

            if (this.IsNegativeInfinity)
            {
                return;
            }

            if (this.IsPositiveInfinity)
            {
                return;
            }

            if (this.IsInteger)
            {
                return;
            }

            if (this.IsZero)
            {
                return;
            }

            if (this.Numerator == 0)
            {
                this.Denominator = 0;
                return;
            }

            if (this.Numerator == this.Denominator)
            {
                this.Numerator = 1;
                this.Denominator = 1;
            }

            long gcd = GreatestCommonDivisor(Math.Abs(this.Numerator), Math.Abs(this.Denominator));
            if (gcd > 1)
            {
                this.Numerator = this.Numerator / gcd;
                this.Denominator = this.Denominator / gcd;
            }
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="rational">
        /// The instance of <see cref="LongRational"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(LongRational rational)
        {
            return ((rational.Numerator * 397) ^ rational.Denominator).GetHashCode();
        }
    }
}