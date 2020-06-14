// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class TolerantMathTests
    {
        private readonly TolerantMath tolerantMath = new TolerantMath(0.1);

        [Theory]
        [InlineData(0)]
        [InlineData(0.01)]
        [InlineData(-0.05)]
        public void IsZero_WhenTrue(double a)
        {
            Assert.True(this.tolerantMath.IsZero(a));
        }

        [Theory]
        [InlineData(0.11)]
        [InlineData(-0.101)]
        [InlineData(42)]
        public void IsZero_WhenFalse(double a)
        {
            Assert.False(this.tolerantMath.IsZero(a));
        }

        [Theory]
        [InlineData(0.11)]
        [InlineData(100)]
        public void IsPositive_WhenTrue(double a)
        {
            Assert.True(this.tolerantMath.IsPositive(a));
        }

        [Theory]
        [InlineData(0.09)]
        [InlineData(-0.1)]
        [InlineData(-1000)]
        public void IsPositive_WhenFalse(double a)
        {
            Assert.False(this.tolerantMath.IsPositive(a));
        }

        [Theory]
        [InlineData(-0.11)]
        [InlineData(-100)]
        public void IsNegative_WhenTrue(double a)
        {
            Assert.True(this.tolerantMath.IsNegative(a));
        }

        [Theory]
        [InlineData(-0.09)]
        [InlineData(0.1)]
        [InlineData(1000)]
        public void IsNegative_WhenFalse(double a)
        {
            Assert.False(this.tolerantMath.IsNegative(a));
        }

        [Theory]
        [InlineData(4.2, 4.2)]
        [InlineData(4.2, 4.25)]
        [InlineData(-Math.PI, -Math.PI + 0.05)]
        [InlineData(999999.2, 999999.25)]
        public void AreEqual_WhenTrue(double a, double b)
        {
            Assert.True(this.tolerantMath.AreEqual(a, b));
        }

        [Theory]
        [InlineData(1, 2)]
        [InlineData(-1000000, -1000000.2)]
        public void AreEqual_WhenFalse(double a, double b)
        {
            Assert.False(this.tolerantMath.AreEqual(a, b));
        }

        [Theory]
        [InlineData(2, 1.8)]
        [InlineData(-20, -20.2)]
        [InlineData(0.1, -0.1)]
        [InlineData(100, 10)]
        public void IsGreater_IsLess_WhenTrue(double a, double b)
        {
            Assert.True(this.tolerantMath.IsGreater(a, b));
            Assert.True(this.tolerantMath.IsLess(b, a));
        }

        [Theory]
        [InlineData(2, 1.95)]
        [InlineData(-20, -20.02)]
        [InlineData(0.01, -0.01)]
        [InlineData(999999, 999999.09)]
        public void IsGreater_IsLess_WhenFalse(double a, double b)
        {
            Assert.False(this.tolerantMath.IsGreater(a, b));
            Assert.False(this.tolerantMath.IsLess(b, a));
        }

        [Theory]
        [InlineData(3, 2)]
        [InlineData(3, 2.99)]
        [InlineData(2.99, 3)]
        [InlineData(-5, -6)]
        [InlineData(-5, -5.05)]
        [InlineData(-5.05, -5)]
        public void IsGreaterOrEqual_IsLessOrEqual_WhenTrue(double a, double b)
        {
            Assert.True(this.tolerantMath.IsGreaterOrEqual(a, b));
            Assert.True(this.tolerantMath.IsLessOrEqual(b, a));
        }

        [Theory]
        [InlineData(2, 3)]
        [InlineData(2.89, 3)]
        [InlineData(-3, -2.89)]
        public void IsGreaterOrEqual_IsLessOrEqual_WhenFalse(double a, double b)
        {
            Assert.False(this.tolerantMath.IsGreaterOrEqual(a, b));
            Assert.False(this.tolerantMath.IsLessOrEqual(b, a));
        }

        [Theory]
        [InlineData(3.5, 4.0)]
        [InlineData(3.89, 4.0)]
        [InlineData(4.09, 4.0)]
        [InlineData(4.11, 5.0)]
        [InlineData(0.11, 1)]
        [InlineData(0.05, 0)]
        [InlineData(-0.5, 0)]
        [InlineData(-0.95, -1)]
        [InlineData(-1.05, -1)]
        [InlineData(-1.5, -1)]
        public void Ceiling(double value, double expected)
        {
            double actual = this.tolerantMath.Ceiling(value);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(0.99, 1)]
        [InlineData(0.5, 0)]
        [InlineData(0.01, 0)]
        [InlineData(-0.09, 0)]
        [InlineData(-0.11, -1)]
        [InlineData(-100.11, -101)]
        [InlineData(-100.09, -100)]
        public void Floor(double value, double expected)
        {
            double plz1 = Math.IEEERemainder(1.1, 1);
            double plz2 = Math.IEEERemainder(0.9, 1);

            double plz3 = Math.IEEERemainder(-1.1, 1);
            double plz4 = Math.IEEERemainder(-0.9, 1);

            double actual = this.tolerantMath.Floor(value);
            Assert.Equal(expected, actual);
        }
    }
}
