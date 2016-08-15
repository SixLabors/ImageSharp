// <copyright file="Rational.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;
    using System.Globalization;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Represents a number that can be expressed as a fraction
    /// </summary>
    /// <remarks>
    /// This is a very simplified implimentation of a rational number designed for use with
    /// metadata only.
    /// </remarks>
    public struct Rational : IEquatable<Rational>
    {
        /// <summary>
        /// Represents a rational object that is not a number. 
        /// </summary>
        public static Rational Indeterminate = new Rational(0, 0);

        /// <summary>
        /// Represents a rational object that is equal to 0. 
        /// </summary>
        public static Rational Zero = new Rational(0, 1);

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
            else if (double.IsPositiveInfinity(value))
            {
                this = PositiveInfinity;
                return;
            }
            else if (double.IsNegativeInfinity(value))
            {
                this = NegativeInfinity;
                return;
            }

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
        public bool IsIndeterminate => (this.Equals(Indeterminate));

        /// <summary>
        /// Gets a value indicating whether this instance is an integer. 
        /// </summary>
        public bool IsInteger => (this.Denominator == 1);

        /// <summary>
        /// Gets a value indicating whether this instance is equal to 0 
        /// </summary>
        public bool IsZero => (this.Equals(Zero));

        /// <summary>
        /// Gets a value indicating whether this instance is equal to 1. 
        /// </summary>
        public bool IsOne => (this.Equals(One));

        /// <summary>
        /// Gets a value indicating whether this instance is equal to negative infinity (-1, 0). 
        /// </summary>
        public bool IsNegativeInfinity => (this.Equals(NegativeInfinity));

        /// <summary>
        /// Gets a value indicating whether this instance is equal to positive infinity (1, 0). 
        /// </summary>
        public bool IsPositiveInfinity => (this.Equals(PositiveInfinity));

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
            if (this.Denominator == other.Denominator)
            {
                return this.Numerator == other.Numerator;
            }
            else if (this.Numerator == BigInteger.Zero && this.Denominator == BigInteger.Zero)
            {
                return other.Numerator == BigInteger.Zero && other.Denominator == BigInteger.Zero;
            }
            else if (other.Numerator == BigInteger.Zero && other.Denominator == BigInteger.Zero)
            {
                return this.Numerator == BigInteger.Zero && this.Denominator == BigInteger.Zero;
            }
            else
            {
                return (this.Numerator * other.Denominator) == (this.Denominator * other.Numerator);
            }
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)this.Numerator * 397) ^ (int)this.Denominator;
            }
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

            if (this.Numerator == BigInteger.Zero)
            {
                Denominator = BigInteger.One;
                return;
            }

            if (this.Numerator == this.Denominator)
            {
                this.Numerator = BigInteger.One;
                this.Denominator = BigInteger.One;
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
            int periodIndex = value.IndexOf(".");
            int eIndeix = value.IndexOf("E");
            int slashIndex = value.IndexOf("/");

            // An integer such as 7
            if (periodIndex == -1 && eIndeix == -1 && slashIndex == -1)
            {
                return new Rational(BigInteger.Parse(value));
            }

            // A fraction such as 3/7
            if (periodIndex == -1 && eIndeix == -1 && slashIndex != -1)
            {
                return new Rational(BigInteger.Parse(value.Substring(0, slashIndex)),
                                    BigInteger.Parse(value.Substring(slashIndex + 1)));
            }

            // No scientific Notation such as 5.997
            if (eIndeix == -1)
            {
                BigInteger n = BigInteger.Parse(value.Replace(".", ""));
                BigInteger d = (BigInteger)Math.Pow(10, value.Length - periodIndex - 1);
                return new Rational(n, d);
            }

            // Scientific notation such as 2.4556E-2
            int characteristic = int.Parse(value.Substring(eIndeix + 1));
            BigInteger ten = 10;
            BigInteger numerator = BigInteger.Parse(value.Substring(0, eIndeix).Replace(".", ""));
            BigInteger denominator = new BigInteger(Math.Pow(10, eIndeix - periodIndex - 1));
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
    }
}