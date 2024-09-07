// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests;

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
        SignedRational r1 = new(3, 2);
        SignedRational r2 = new(3, 2);

        Assert.Equal(r1, r2);
        Assert.True(r1 == r2);

        SignedRational r3 = new(7.55);
        SignedRational r4 = new(755, 100);
        SignedRational r5 = new(151, 20);

        Assert.Equal(r3, r4);
        Assert.Equal(r4, r5);
    }

    /// <summary>
    /// Tests the equality operators for inequality.
    /// </summary>
    [Fact]
    public void AreNotEqual()
    {
        SignedRational first = new(0, 100);
        SignedRational second = new(100, 100);

        Assert.NotEqual(first, second);
        Assert.True(first != second);
    }

    /// <summary>
    /// Tests whether the Rational constructor correctly assign properties.
    /// </summary>
    [Fact]
    public void ConstructorAssignsProperties()
    {
        SignedRational rational = new(7, -55);
        Assert.Equal(7, rational.Numerator);
        Assert.Equal(-55, rational.Denominator);

        rational = new(-755, 100);
        Assert.Equal(-151, rational.Numerator);
        Assert.Equal(20, rational.Denominator);

        rational = new(-755, -100, false);
        Assert.Equal(-755, rational.Numerator);
        Assert.Equal(-100, rational.Denominator);

        rational = new(-151, -20);
        Assert.Equal(-151, rational.Numerator);
        Assert.Equal(-20, rational.Denominator);

        rational = new(-7.55);
        Assert.Equal(-151, rational.Numerator);
        Assert.Equal(20, rational.Denominator);

        rational = new(7);
        Assert.Equal(7, rational.Numerator);
        Assert.Equal(1, rational.Denominator);
    }

    [Fact]
    public void Fraction()
    {
        SignedRational first = new(1.0 / 1600);
        SignedRational second = new(1.0 / 1600, true);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void ToDouble()
    {
        SignedRational rational = new(0, 0);
        Assert.Equal(double.NaN, rational.ToDouble());

        rational = new(2, 0);
        Assert.Equal(double.PositiveInfinity, rational.ToDouble());

        rational = new(-2, 0);
        Assert.Equal(double.NegativeInfinity, rational.ToDouble());
    }

    [Fact]
    public void ToStringRepresentation()
    {
        SignedRational rational = new(0, 0);
        Assert.Equal("[ Indeterminate ]", rational.ToString());

        rational = new(double.PositiveInfinity);
        Assert.Equal("[ PositiveInfinity ]", rational.ToString());

        rational = new(double.NegativeInfinity);
        Assert.Equal("[ NegativeInfinity ]", rational.ToString());

        rational = new(0, 1);
        Assert.Equal("0", rational.ToString());

        rational = new(2, 1);
        Assert.Equal("2", rational.ToString());

        rational = new(1, 2);
        Assert.Equal("1/2", rational.ToString());
    }
}
