// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Represents a number that can be expressed as a fraction.
    /// </summary>
    /// <remarks>
    /// This is a very simplified implementation of a rational number designed for use with metadata only.
    /// </remarks>
    public readonly struct SignedRational : IEquatable<SignedRational>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="int"/> to create the rational from.</param>
        public SignedRational(int value)
          : this(value, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="numerator">The number above the line in a vulgar fraction showing how many of the parts indicated by the denominator are taken.</param>
        /// <param name="denominator">The number below the line in a vulgar fraction; a divisor.</param>
        public SignedRational(int numerator, int denominator)
          : this(numerator, denominator, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="numerator">The number above the line in a vulgar fraction showing how many of the parts indicated by the denominator are taken.</param>
        /// <param name="denominator">The number below the line in a vulgar fraction; a divisor.</param>
        /// <param name="simplify">Specified if the rational should be simplified.</param>
        public SignedRational(int numerator, int denominator, bool simplify)
        {
            if (simplify)
            {
                var rational = new LongRational(numerator, denominator).Simplify();

                this.Numerator = (int)rational.Numerator;
                this.Denominator = (int)rational.Denominator;
            }
            else
            {
                this.Numerator = numerator;
                this.Denominator = denominator;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        public SignedRational(double value)
          : this(value, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        public SignedRational(double value, bool bestPrecision)
        {
            var rational = LongRational.FromDouble(value, bestPrecision);

            this.Numerator = (int)rational.Numerator;
            this.Denominator = (int)rational.Denominator;
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public int Numerator { get; }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public int Denominator { get; }

        /// <summary>
        /// Determines whether the specified <see cref="SignedRational"/> instances are considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="SignedRational"/>  to compare.</param>
        /// <param name="right"> The second <see cref="SignedRational"/>  to compare.</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool operator ==(SignedRational left, SignedRational right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether the specified <see cref="SignedRational"/> instances are not considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="SignedRational"/> to compare.</param>
        /// <param name="right"> The second <see cref="SignedRational"/> to compare.</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool operator !=(SignedRational left, SignedRational right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        /// <returns>
        /// The <see cref="SignedRational"/>.
        /// </returns>
        public static SignedRational FromDouble(double value)
        {
            return new SignedRational(value, false);
        }

        /// <summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        /// <returns>
        /// The <see cref="SignedRational"/>.
        /// </returns>
        public static SignedRational FromDouble(double value, bool bestPrecision)
        {
            return new SignedRational(value, bestPrecision);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is SignedRational other && this.Equals(other);
        }

        /// <inheritdoc/>
        public bool Equals(SignedRational other)
        {
            var left = new LongRational(this.Numerator, this.Denominator);
            var right = new LongRational(other.Numerator, other.Denominator);

            return left.Equals(right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var self = new LongRational(this.Numerator, this.Denominator);
            return self.GetHashCode();
        }

        /// <summary>
        /// Converts a rational number to the nearest <see cref="double"/>.
        /// </summary>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public double ToDouble()
        {
            return this.Numerator / (double)this.Denominator;
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
            var rational = new LongRational(this.Numerator, this.Denominator);
            return rational.ToString(provider);
        }
    }
}
