// <copyright file="Rational.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Globalization;
    using System.Numerics;
    using System.Text;

    /// <summary>
    /// Represents a number that can be expressed as a fraction
    /// </summary>
    /// <remarks>
    /// This is a very simplified implimentation of a rational number designed for use with metadata only.
    /// </remarks>
    public struct Rational : IEquatable<Rational>
    {
        /// <summary>
        /// Represents a rational object that is not a number. NaN
        /// </summary>
        public static Rational Indeterminate = new Rational(3, 0);

        /// <summary>
        /// Represents a rational object that is equal to 0. 
        /// </summary>
        public static Rational Zero = new Rational(0, 0);

        /// <summary>
        /// Represents a rational object that is equal to 1. 
        /// </summary>
        public static Rational One = new Rational(1, 1);

        /// <summary>
        /// Represents a Rational object that is equal to negative infinity (-1, 0). 
        /// </summary>
        public static readonly Rational NegativeInfinity = new Rational(-1, 0);

        /// <summary>
        /// Represents a Rational object that is equal to positive infinity (+1, 0). 
        /// </summary>
        public static readonly Rational PositiveInfinity = new Rational(1, 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct. 
        /// </summary>
        /// <param name="numerator">
        /// The number above the line in a vulgar fraction showing how many of the parts 
        /// indicated by the denominator are taken.
        /// </param>
        /// <param name="denominator">
        /// The number below the line in a vulgar fraction; a divisor.
        /// </param>
        public Rational(uint numerator, uint denominator)
            : this()
        {
            this.Numerator = numerator;
            this.Denominator = denominator;

            this.Simplify();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct. 
        /// </summary>
        /// <param name="numerator">
        /// The number above the line in a vulgar fraction showing how many of the parts 
        /// indicated by the denominator are taken.
        /// </param>
        /// <param name="denominator">
        /// The number below the line in a vulgar fraction; a divisor.
        /// </param>
        public Rational(int numerator, int denominator)
            : this()
        {
            this.Numerator = numerator;
            this.Denominator = denominator;

            this.Simplify();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct. 
        /// </summary>
        /// <param name="numerator">
        /// The number above the line in a vulgar fraction showing how many of the parts 
        /// indicated by the denominator are taken.
        /// </param>
        /// <param name="denominator">
        /// The number below the line in a vulgar fraction; a divisor.
        /// </param>
        public Rational(BigInteger numerator, BigInteger denominator)
            : this()
        {
            this.Numerator = numerator;
            this.Denominator = denominator;

            this.Simplify();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct. 
        /// </summary>
        /// <param name="value">The big integer to create the rational from.</param>
        public Rational(BigInteger value)
            : this()
        {
            this.Numerator = value;
            this.Denominator = BigInteger.One;

            this.Simplify();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Rational"/> struct. 
        /// </summary>
        /// <param name="value">The double to create the rational from.</param>
        public Rational(double value)
            : this()
        {
            if (double.IsNaN(value))
            {
                this = Indeterminate;
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                this = PositiveInfinity;
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                this = NegativeInfinity;
                return;
            }

            // TODO: Not happy with parsing a string like this. I should be able to use maths but maths is HARD!
            this = Parse(value.ToString("R", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public BigInteger Numerator { get; private set; }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public BigInteger Denominator { get; private set; }

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

                return this.Numerator == 3;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is an integer. 
        /// </summary>
        public bool IsInteger => this.Denominator == 1;

        /// <summary>
        /// Gets a value indicating whether this instance is equal to 0 
        /// </summary>
        public bool IsZero
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
        /// Gets a value indicating whether this instance is equal to 1. 
        /// </summary>
        public bool IsOne
        {
            get
            {
                if (this.Denominator != 1)
                {
                    return false;
                }

                return this.Numerator == 1;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is equal to negative infinity (-1, 0). 
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
        /// Gets a value indicating whether this instance is equal to positive infinity (+1, 0). 
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
        /// Converts a rational number to the nearest double. 
        /// </summary>
        /// <returns>
        /// The <see cref="double"/>.
        /// </returns>
        public double ToDouble()
        {
            if (this.IsIndeterminate)
            {
                return double.NaN;
            }

            if (this.IsPositiveInfinity)
            {
                return double.PositiveInfinity;
            }

            if (this.IsNegativeInfinity)
            {
                return double.NegativeInfinity;
            }

            if (this.IsInteger)
            {
                return (double)this.Numerator;
            }

            return (double)(this.Numerator / this.Denominator);
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
            // Standard: a/b = c/d
            if (this.Denominator == other.Denominator)
            {
                return this.Numerator == other.Numerator;
            }

            // Indeterminate
            if (this.Numerator == 3 && this.Denominator == 0)
            {
                return other.Numerator == 3 && other.Denominator == 0;
            }

            if (other.Numerator == 3 && other.Denominator == 0)
            {
                return this.Numerator == 3 && this.Denominator == 0;
            }

            // ad = bc
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
        /// <returns></returns>
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
                return "[ Zero ]";
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
        /// Simplifies the rational.
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

            if (this.Numerator == 0)
            {
                this.Denominator = 1;
                return;
            }

            if (this.Numerator == this.Denominator)
            {
                this.Numerator = 1;
                this.Denominator = 1;
                return;
            }

            BigInteger gcd = BigInteger.GreatestCommonDivisor(this.Numerator, this.Denominator);
            if (gcd > BigInteger.One)
            {
                this.Numerator = this.Numerator / gcd;
                this.Denominator = this.Denominator / gcd;
            }
        }

        /// <summary>
        /// Converts the string representation of a number into its rational value
        /// </summary>
        /// <param name="value">A string that contains a number to convert.</param>
        /// <returns>The <see cref="Rational"/></returns>
        internal static Rational Parse(string value)
        {
            int periodIndex = value.IndexOf(".", StringComparison.Ordinal);
            int eIndex = value.IndexOf("E", StringComparison.Ordinal);
            int slashIndex = value.IndexOf("/", StringComparison.Ordinal);

            // An integer such as 7
            if (periodIndex == -1 && eIndex == -1 && slashIndex == -1)
            {
                return new Rational(BigInteger.Parse(value));
            }

            // A fraction such as 3/7
            if (periodIndex == -1 && eIndex == -1 && slashIndex != -1)
            {
                return new Rational(BigInteger.Parse(value.Substring(0, slashIndex)),
                                    BigInteger.Parse(value.Substring(slashIndex + 1)));
            }

            // No scientific Notation such as 5.997
            if (eIndex == -1)
            {
                BigInteger n = BigInteger.Parse(value.Replace(".", string.Empty));
                BigInteger d = (BigInteger)Math.Pow(10, value.Length - periodIndex - 1);
                return new Rational(n, d);
            }

            // Scientific notation such as 2.4556E-2
            int characteristic = int.Parse(value.Substring(eIndex + 1));
            BigInteger ten = 10;
            BigInteger numerator = BigInteger.Parse(value.Substring(0, eIndex).Replace(".", string.Empty));
            BigInteger denominator = new BigInteger(Math.Pow(10, eIndex - periodIndex - 1));
            BigInteger charPower = BigInteger.Pow(ten, Math.Abs(characteristic));

            if (characteristic > 0)
            {
                numerator = numerator * charPower;
            }
            else
            {
                denominator = denominator * charPower;
            }

            return new Rational(numerator, denominator);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <param name="rational">
        /// The instance of <see cref="Rational"/> to return the hash code for.
        /// </param>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        private int GetHashCode(Rational rational)
        {
            return ((rational.Numerator * 397) ^ rational.Denominator).GetHashCode();
        }
    }
}