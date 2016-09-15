// <copyright file="Rational.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Represents a number that can be expressed as a fraction.
    /// </summary>
    /// <remarks>
    /// This is a very simplified implementation of a rational number designed for use with metadata only.
    /// </remarks>
    public struct Rational : IEquatable<Rational>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="uint"/> to create the rational from.</param>
        public Rational(uint value)
          : this(value, 1)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct.
        /// </summary>
        /// <param name="numerator">The number above the line in a vulgar fraction showing how many of the parts indicated by the denominator are taken.</param>
        /// <param name="denominator">The number below the line in a vulgar fraction; a divisor.</param>
        public Rational(uint numerator, uint denominator)
          : this(numerator, denominator, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct.
        /// </summary>
        /// <param name="numerator">The number above the line in a vulgar fraction showing how many of the parts indicated by the denominator are taken.</param>
        /// <param name="denominator">The number below the line in a vulgar fraction; a divisor.</param>
        /// <param name="simplify">Specified if the rational should be simplified.</param>
        public Rational(uint numerator, uint denominator, bool simplify)
        {
            LongRational rational = new LongRational(numerator, denominator, simplify);

            this.Numerator = (uint)rational.Numerator;
            this.Denominator = (uint)rational.Denominator;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        public Rational(double value)
          : this(value, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to create the instance from.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        public Rational(double value, bool bestPrecision)
        {
            LongRational rational = new LongRational(Math.Abs(value), bestPrecision);

            this.Numerator = (uint)rational.Numerator;
            this.Denominator = (uint)rational.Denominator;
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public uint Numerator { get; }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public uint Denominator { get; }

        /// <summary>
        /// Determines whether the specified <see cref="Rational"/> instances are considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="Rational"/>  to compare.</param>
        /// <param name="right"> The second <see cref="Rational"/>  to compare.</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool operator ==(Rational left, Rational right)
        {
            return Rational.Equals(left, right);
        }

        /// <summary>
        /// Determines whether the specified <see cref="Rational"/> instances are not considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="Rational"/> to compare.</param>
        /// <param name="right"> The second <see cref="Rational"/> to compare.</param>
        /// <returns>The <see cref="bool"/></returns>
        public static bool operator !=(Rational left, Rational right)
        {
            return !Rational.Equals(left, right);
        }

        /// <summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        /// <returns>
        /// The <see cref="Rational"/>.
        /// </returns>
        public static Rational FromDouble(double value)
        {
            return new Rational(value, false);
        }

        /// <summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        /// </summary>
        /// <param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        /// <param name="bestPrecision">Whether to use the best possible precision when parsing the value.</param>
        /// <returns>
        /// The <see cref="Rational"/>.
        /// </returns>
        public static Rational FromDouble(double value, bool bestPrecision)
        {
            return new Rational(value, bestPrecision);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (obj is Rational)
            {
                return this.Equals((Rational)obj);
            }

            return false;
        }

        /// <inheritdoc/>
        public bool Equals(Rational other)
        {
            LongRational left = new LongRational(this.Numerator, this.Denominator);
            LongRational right = new LongRational(other.Numerator, other.Denominator);

            return left.Equals(right);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            LongRational self = new LongRational(this.Numerator, this.Denominator);
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
            LongRational rational = new LongRational(this.Numerator, this.Denominator);
            return rational.ToString(provider);
        }
    }
}