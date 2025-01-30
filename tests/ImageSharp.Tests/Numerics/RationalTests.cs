// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests;

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
        Rational r1 = new(3, 2);
        Rational r2 = new(3, 2);

        Assert.Equal(r1, r2);
        Assert.True(r1 == r2);

        Rational r3 = new(7.55);
        Rational r4 = new(755, 100);
        Rational r5 = new(151, 20);

        Assert.Equal(r3, r4);
        Assert.Equal(r4, r5);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        Rational first = new(0, 100);
        Rational second = new(100, 100);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    /// <summary>
    /// Tests known out-of-range values.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="numerator">The expected numerator.</param>
    /// <param name="denominator">The expected denominator.</param>
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(double.NaN, 0, 0)]
    [InlineData(double.PositiveInfinity, 1, 0)]
    [InlineData(double.NegativeInfinity, 1, 0)]
    public void FromDoubleOutOfRange(double value, uint numerator, uint denominator)
    {
        Rational r = Rational.FromDouble(value);

        Assert.Equal(numerator, r.Numerator);
        Assert.Equal(denominator, r.Denominator);
    }

    /// <summary>
    /// Tests whether the Rational constructor correctly assign properties.
    /// </summary>
    [Fact]
    public void ConstructorAssignsProperties()
    {
        Rational rational = new(7, 55);
        Assert.Equal(7U, rational.Numerator);
        Assert.Equal(55U, rational.Denominator);

        rational = new(755, 100);
        Assert.Equal(151U, rational.Numerator);
        Assert.Equal(20U, rational.Denominator);

        rational = new(755, 100, false);
        Assert.Equal(755U, rational.Numerator);
        Assert.Equal(100U, rational.Denominator);

        rational = new(-7.55);
        Assert.Equal(151U, rational.Numerator);
        Assert.Equal(20U, rational.Denominator);

        rational = new(7);
        Assert.Equal(7U, rational.Numerator);
        Assert.Equal(1U, rational.Denominator);
    }

    [Fact]
    public void Fraction()
    {
        Rational first = new(1.0 / 1600);
        Rational second = new(1.0 / 1600, true);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void ToDouble()
    {
        Rational rational = new(0, 0);
        Assert.Equal(double.NaN, rational.ToDouble());

        rational = new(2, 0);
        Assert.Equal(double.PositiveInfinity, rational.ToDouble());
    }

    [Fact]
    public void ToStringRepresentation()
    {
        Rational rational = new(0, 0);
        Assert.Equal("[ Indeterminate ]", rational.ToString());

        rational = new(double.PositiveInfinity);
        Assert.Equal("[ PositiveInfinity ]", rational.ToString());

        rational = new(double.NegativeInfinity);
        Assert.Equal("[ PositiveInfinity ]", rational.ToString());

        rational = new(0, 1);
        Assert.Equal("0", rational.ToString());

        rational = new(2, 1);
        Assert.Equal("2", rational.ToString());

        rational = new(1, 2);
        Assert.Equal("1/2", rational.ToString());
    }
}
