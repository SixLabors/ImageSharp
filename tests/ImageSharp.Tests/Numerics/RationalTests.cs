// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Tests;

/// <summary>
/// Tests the <see cref="Rational"/> struct.
/// </summary>
public class RationalTests
{
    /// <summary>
    /// Verifies exact fractions, component limits, and special values in both conversion modes.
    /// </summary>
    /// <param name="value">The input value.</param>
    /// <param name="numerator">The expected numerator.</param>
    /// <param name="denominator">The expected denominator.</param>
    [Theory]
    [InlineData(0.75, 3L, 4L)]
    [InlineData(-0.75, -3L, 4L)]
    [InlineData(4294967296D, 4294967296L, 1L)]
    [InlineData(18014398509481984D, 18014398509481984L, 1L)]
    [InlineData(-9223372036854775808D, long.MinValue, 1L)]
    [InlineData(9223372036854775808D, long.MaxValue, 1L)]
    [InlineData(double.MaxValue, long.MaxValue, 1L)]
    [InlineData(double.MinValue, long.MinValue, 1L)]
    [InlineData(double.Epsilon, 0L, 1L)]
    [InlineData(0D, 0L, 1L)]
    [InlineData(double.NaN, 0L, 0L)]
    [InlineData(double.PositiveInfinity, 1L, 0L)]
    [InlineData(double.NegativeInfinity, -1L, 0L)]
    public void LongRationalFromDouble(double value, long numerator, long denominator)
    {
        LongRational expected = new(numerator, denominator);

        Assert.Equal(expected, LongRational.FromDouble(value, false));
        Assert.Equal(expected, LongRational.FromDouble(value, true));
    }

    /// <summary>
    /// Verifies that denominator limits select a finite approximation without overflowing either component.
    /// </summary>
    [Fact]
    public void FromDoubleRespectsDenominatorLimit()
    {
        // The nearest representable fraction to 2^-32 is 1/(2^32 - 1).
        // Zero is farther away, and every larger numerator increases the error at this denominator limit.
        Rational value = new(1D / 4294967296D, true);

        Assert.Equal(1U, value.Numerator);
        Assert.Equal(uint.MaxValue, value.Denominator);
    }

    /// <summary>
    /// Verifies that exact conversion does not lose precision through rounded reciprocals near one.
    /// </summary>
    [Fact]
    public void LongRationalFromDoubleRetainsAdjacentValue()
    {
        // The preceding double is exactly (2^53 - 1)/2^53. Both components fit in a long.
        LongRational value = LongRational.FromDouble(double.BitDecrement(1D), true);

        Assert.Equal(new LongRational(9007199254740991L, 9007199254740992L), value);
    }

    /// <summary>
    /// Verifies that 64-bit metadata fractions simplify without overflowing signed component magnitudes.
    /// </summary>
    /// <param name="numerator">The original numerator.</param>
    /// <param name="denominator">The original denominator.</param>
    /// <param name="expectedNumerator">The reduced numerator.</param>
    /// <param name="expectedDenominator">The reduced denominator.</param>
    [Theory]
    [InlineData(long.MinValue, 2L, long.MinValue / 2, 1L)]
    [InlineData(2L, long.MinValue, 1L, long.MinValue / 2)]
    [InlineData(0L, long.MinValue, 0L, -1L)]
    [InlineData(long.MinValue, 0L, -1L, 0L)]
    [InlineData(long.MinValue, long.MinValue, 1L, 1L)]
    public void LongRationalSimplifiesSignedLimits(long numerator, long denominator, long expectedNumerator, long expectedDenominator)
    {
        LongRational value = new(numerator, denominator);

        Assert.Equal(new LongRational(expectedNumerator, expectedDenominator), value.Simplify());
    }

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
    [InlineData(4294967296D, uint.MaxValue, 1)]
    [InlineData(-4294967296D, uint.MaxValue, 1)]
    [InlineData(double.MaxValue, uint.MaxValue, 1)]
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
        Rational first = new(1.0 / 1600);
        Rational second = new(1.0 / 1600, true);
        Assert.False(first.Equals(second));
    }

    [Fact]
    public void ToDouble()
    {
        Rational rational = new(0, 0);
        Assert.Equal(double.NaN, rational.ToDouble());

        rational = new Rational(2, 0);
        Assert.Equal(double.PositiveInfinity, rational.ToDouble());
    }

    [Fact]
    public void ToStringRepresentation()
    {
        Rational rational = new(0, 0);
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
