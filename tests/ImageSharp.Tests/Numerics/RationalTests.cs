// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.MetaData.Profiles.Exif;
using SixLabors.ImageSharp.Primitives;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Rational"/> struct.
    /// </summary>
    public class RationalTests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var r1 = new Rational(3, 2);
            var r2 = new Rational(3, 2);

            Assert.Equal(r1, r2);
            Assert.True(r1 == r2);

            var r3 = new Rational(7.55);
            var r4 = new Rational(755, 100);
            var r5 = new Rational(151, 20);

            Assert.Equal(r3, r4);
            Assert.Equal(r4, r5);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var first = new Rational(0, 100);
            var second = new Rational(100, 100);

            Assert.NotEqual(first, second);
            Assert.True(first != second);
        }

        /// <summary>
        /// Tests whether the Rational constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var rational = new Rational(7, 55);
            Assert.Equal(7U, rational.Numerator);
            Assert.Equal(55U, rational.Denominator);

            rational = new Rational(755, 100);
            Assert.Equal(151U, rational.Numerator);
            Assert.Equal(20U, rational.Denominator);

            rational = new Rational(755, 100, false);
            Assert.Equal(755U, rational.Numerator);
            Assert.Equal(100U, rational.Denominator);

            rational = new Rational(-7.55);
            Assert.Equal(151U, rational.Numerator);
            Assert.Equal(20U, rational.Denominator);

            rational = new Rational(7);
            Assert.Equal(7U, rational.Numerator);
            Assert.Equal(1U, rational.Denominator);
        }

        [Fact]
        public void Fraction()
        {
            var first = new Rational(1.0 / 1600);
            var second = new Rational(1.0 / 1600, true);
            Assert.False(first.Equals(second));
        }

        [Fact]
        public void ToDouble()
        {
            var rational = new Rational(0, 0);
            Assert.Equal(double.NaN, rational.ToDouble());

            rational = new Rational(2, 0);
            Assert.Equal(double.PositiveInfinity, rational.ToDouble());
        }

        [Fact]
        public void ToStringRepresention()
        {
            var rational = new Rational(0, 0);
            Assert.Equal("[ Indeterminate ]", rational.ToString());

            rational = new Rational(double.PositiveInfinity);
            Assert.Equal("[ PositiveInfinity ]", rational.ToString());

            rational = new Rational(double.NegativeInfinity);
            Assert.Equal("[ PositiveInfinity ]", rational.ToString());

            rational = new Rational(0, 1);
            Assert.Equal("0", rational.ToString());

            rational = new Rational(2, 1);
            Assert.Equal("2", rational.ToString());

            rational = new Rational(1, 2);
            Assert.Equal("1/2", rational.ToString());
        }
    }
}