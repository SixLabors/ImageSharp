// <copyright file="RationalTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="SignedRational"/> struct.
    /// </summary>
    public class SignedRationalTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            SignedRational r1 = new SignedRational(3, 2);
            SignedRational r2 = new SignedRational(3, 2);

            Assert.Equal(r1, r2);
            Assert.True(r1 == r2);

            SignedRational r3 = new SignedRational(7.55);
            SignedRational r4 = new SignedRational(755, 100);
            SignedRational r5 = new SignedRational(151, 20);

            Assert.Equal(r3, r4);
            Assert.Equal(r4, r5);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            SignedRational first = new SignedRational(0, 100);
            SignedRational second = new SignedRational(100, 100);

            Assert.NotEqual(first, second);
            Assert.True(first != second);
        }

        /// <summary>
        /// Tests whether the Rational constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            SignedRational rational = new SignedRational(7, -55);
            Assert.Equal(7, rational.Numerator);
            Assert.Equal(-55, rational.Denominator);

            rational = new SignedRational(-755, 100);
            Assert.Equal(-151, rational.Numerator);
            Assert.Equal(20, rational.Denominator);

            rational = new SignedRational(-755, -100, false);
            Assert.Equal(-755, rational.Numerator);
            Assert.Equal(-100, rational.Denominator);

            rational = new SignedRational(-151, -20);
            Assert.Equal(-151, rational.Numerator);
            Assert.Equal(-20, rational.Denominator);

            rational = new SignedRational(-7.55);
            Assert.Equal(-151, rational.Numerator);
            Assert.Equal(20, rational.Denominator);

            rational = new SignedRational(7);
            Assert.Equal(7, rational.Numerator);
            Assert.Equal(1, rational.Denominator);
        }

        [Fact]
        public void Fraction()
        {
            SignedRational first = new SignedRational(1.0 / 1600);
            SignedRational second = new SignedRational(1.0 / 1600, true);
            Assert.False(first.Equals(second));
        }

        [Fact]
        public void ToDouble()
        {
            SignedRational rational = new SignedRational(0, 0);
            Assert.Equal(double.NaN, rational.ToDouble());

            rational = new SignedRational(2, 0);
            Assert.Equal(double.PositiveInfinity, rational.ToDouble());

            rational = new SignedRational(-2, 0);
            Assert.Equal(double.NegativeInfinity, rational.ToDouble());
        }

        [Fact]
        public void ToStringRepresention()
        {
            SignedRational rational = new SignedRational(0, 0);
            Assert.Equal("[ Indeterminate ]", rational.ToString());

            rational = new SignedRational(double.PositiveInfinity);
            Assert.Equal("[ PositiveInfinity ]", rational.ToString());

            rational = new SignedRational(double.NegativeInfinity);
            Assert.Equal("[ NegativeInfinity ]", rational.ToString());

            rational = new SignedRational(0, 1);
            Assert.Equal("0", rational.ToString());

            rational = new SignedRational(2, 1);
            Assert.Equal("2", rational.ToString());

            rational = new SignedRational(1, 2);
            Assert.Equal("1/2", rational.ToString());
        }
    }
}