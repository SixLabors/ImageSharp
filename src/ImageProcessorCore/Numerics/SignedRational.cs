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
    /// This is a very simplified implimentation of a rational number designed for use with metadata only.
    /// </remarks>
    public struct SignedRational : IEquatable<SignedRational>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        ///<param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        public SignedRational(double value)
          : this(value, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        ///<param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        ///<param name="bestPrecision">Specifies if the instance should be created with the best precision possible.</param>
        public SignedRational(double value, bool bestPrecision)
        {
            BigRational rational = new BigRational(value, bestPrecision);

            Numerator = (int)rational.Numerator;
            Denominator = (int)rational.Denominator;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SignedRational"/> struct.
        /// </summary>
        /// <param name="value">The integer to create the rational from.</param>
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
            BigRational rational = new BigRational(numerator, denominator, simplify);

            Numerator = (int)rational.Numerator;
            Denominator = (int)rational.Denominator;
        }

        /// <summary>
        /// Determines whether the specified <see cref="SignedRational"/> instances are considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="SignedRational"/>  to compare.</param>
        /// <param name="right"> The second <see cref="SignedRational"/>  to compare.</param>
        /// <returns></returns>
        public static bool operator ==(SignedRational left, SignedRational right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Determines whether the specified <see cref="SignedRational"/> instances are not considered equal.
        /// </summary>
        /// <param name="left">The first <see cref="SignedRational"/> to compare.</param>
        /// <param name="right"> The second <see cref="SignedRational"/> to compare.</param>
        /// <returns></returns>
        public static bool operator !=(SignedRational left, SignedRational right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Gets the numerator of a number.
        /// </summary>
        public int Numerator
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the denominator of a number.
        /// </summary>
        public int Denominator
        {
            get;
            private set;
        }

        ///<summary>
        /// Determines whether the specified <see cref="object"/> is equal to this <see cref="SignedRational"/>.
        ///</summary>
        ///<param name="obj">The <see cref="object"/> to compare this <see cref="SignedRational"/> with.</param>
        public override bool Equals(object obj)
        {
            if (obj is SignedRational)
                return Equals((SignedRational)obj);

            return false;
        }

        ///<summary>
        /// Determines whether the specified <see cref="SignedRational"/> is equal to this <see cref="SignedRational"/>.
        ///</summary>
        ///<param name="other">The <see cref="SignedRational"/> to compare this <see cref="SignedRational"/> with.</param>
        public bool Equals(SignedRational other)
        {
            BigRational left = new BigRational(Numerator, Denominator);
            BigRational right = new BigRational(other.Numerator, other.Denominator);

            return left.Equals(right);
        }

        ///<summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        ///</summary>
        ///<param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        public static SignedRational FromDouble(double value)
        {
            return new SignedRational(value, false);
        }

        ///<summary>
        /// Converts the specified <see cref="double"/> to an instance of this type.
        ///</summary>
        ///<param name="value">The <see cref="double"/> to convert to an instance of this type.</param>
        ///<param name="bestPrecision">Specifies if the instance should be created with the best precision possible.</param>
        public static SignedRational FromDouble(double value, bool bestPrecision)
        {
            return new SignedRational(value, bestPrecision);
        }

        ///<summary>
        /// Serves as a hash of this type.
        ///</summary>
        public override int GetHashCode()
        {
            BigRational self = new BigRational(Numerator, Denominator);
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
            return Numerator / (double)Denominator;
        }

        /// <summary>
        /// Converts the numeric value of this instance to its equivalent string representation.
        /// </summary>
        public override string ToString()
        {
            return ToString(CultureInfo.InvariantCulture);
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
            BigRational rational = new BigRational(Numerator, Denominator);
            return rational.ToString(provider);
        }
    }
}