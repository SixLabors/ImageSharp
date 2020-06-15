// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
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
            var r1 = new SignedRational(3, 2);
            var r2 = new SignedRational(3, 2);

            Assert.Equal(r1, r2);
            Assert.True(r1 == r2);

            var r3 = new SignedRational(7.55);
            var r4 = new SignedRational(755, 100);
            var r5 = new SignedRational(151, 20);

            Assert.Equal(r3, r4);
            Assert.Equal(r4, r5);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var first = new SignedRational(0, 100);
            var second = new SignedRational(100, 100);

            Assert.NotEqual(first, second);
            Assert.True(first != second);
        }

        /// <summary>
        /// Tests whether the Rational constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var rational = new SignedRational(7, -55);
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
            var first = new SignedRational(1.0 / 1600);
            var second = new SignedRational(1.0 / 1600, true);
            Assert.False(first.Equals(second));
        }

        [Fact]
        public void ToDouble()
        {
            var rational = new SignedRational(0, 0);
            Assert.Equal(double.NaN, rational.ToDouble());

            rational = new SignedRational(2, 0);
            Assert.Equal(double.PositiveInfinity, rational.ToDouble());

            rational = new SignedRational(-2, 0);
            Assert.Equal(double.NegativeInfinity, rational.ToDouble());
        }

        [Fact]
        public void ToStringRepresentation()
        {
            var rational = new SignedRational(0, 0);
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
