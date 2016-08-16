// <copyright file="RationalTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Xunit;

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
            Rational r1 = new Rational(3, 2);
            Rational r2 = new Rational(3, 2);

            Assert.Equal(r1, r2);

            Rational r3 = new Rational(7.55);
            Rational r4 = new Rational(755, 100);
            Rational r5 = new Rational(151, 20);

            Assert.Equal(r3, r4);
            Assert.Equal(r4, r5);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Rational first = new Rational(0, 100);
            Rational second = new Rational(100, 100);

            Assert.NotEqual(first, second);
        }

        /// <summary>
        /// Tests whether the Rational constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Rational first = new Rational(4, 5);
            Assert.Equal(4, first.Numerator);
            Assert.Equal(5, first.Denominator);
        }
    }
}